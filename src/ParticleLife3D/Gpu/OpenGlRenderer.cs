using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using OpenTK.GLControl;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using ParticleLife3D.Models;
using ParticleLife3D.Utils;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;
using AppContext = ParticleLife3D.Models.AppContext;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using Panel = System.Windows.Controls.Panel;
using PixelFormat = OpenTK.Graphics.OpenGL.PixelFormat;

namespace ParticleLife3D.Gpu
{
    public class OpenGlRenderer
    {
        public const float ForwardSpeed = 0.1f;

        public const float DirectionChangeSpeed = 0.01f;
        public int FrameCounter => frameCounter;

        public bool Paused { get; set; }

        public int? TrackedIdx { get; set; }

        private Panel placeholder;

        private System.Windows.Forms.Integration.WindowsFormsHost host;

        private GLControl glControl;

        private int frameCounter;

        private ComputeProgram computeProgram;

        private DisplayProgram displayProgram;

        private Vector4 center;

        private double xzAngle = 0;

        private double yAngle = 0;

        private AppContext app;

        public byte[] captureBuffer;

        private int? recFrameNr;

        public OpenGlRenderer(Panel placeholder, AppContext app)
        {
            this.placeholder = placeholder;
            this.app = app;
            host = new System.Windows.Forms.Integration.WindowsFormsHost();
            host.Visibility = Visibility.Visible;
            host.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            host.VerticalAlignment = VerticalAlignment.Stretch;
            glControl = new GLControl(new GLControlSettings
            {
                API = OpenTK.Windowing.Common.ContextAPI.OpenGL,
                APIVersion = new Version(3, 3), // OpenGL 3.3
                Profile = ContextProfile.Compatability,
                Flags = ContextFlags.Default,
                IsEventDriven = false
            });
            glControl.Dock = DockStyle.Fill;
            host.Child = glControl;
            placeholder.Children.Add(host);

            //setup required features
            GL.Enable(EnableCap.ProgramPointSize);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
            GL.BlendEquation(OpenTK.Graphics.OpenGL.BlendEquationMode.FuncAdd);
            GL.Enable(EnableCap.PointSprite);

            computeProgram = new ComputeProgram();
            displayProgram = new DisplayProgram();
            UploadParticleData();
            ResetOrigin();

            var dragging = new DraggingHandler(glControl, (mousePos, isLeft) => isLeft, (prev, curr) =>
            {
                var delta = (curr - prev);
                if (System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftShift) ||
                    System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftShift))
                {
                    // change camera angle
                    xzAngle += (delta.X) * DirectionChangeSpeed;
                    yAngle += (delta.Y) * DirectionChangeSpeed;
                    yAngle = Math.Clamp(yAngle, -Math.PI*0.48, Math.PI * 0.48);
                }
                else
                {
                    // translating camera in a plane perpendicular to the current cammera direction
                    StopTracking();
                    var forward = GetCameraDirection();
                    forward.Normalize();
                    Vector3 right = Vector3.Normalize(Vector3.Cross(forward.Xyz, Vector3.UnitY));
                    Vector3 up = Vector3.Cross(right, forward.Xyz);
                    var trranslation = -right * delta.X + up * delta.Y;
                    center += new Vector4(trranslation.X, trranslation.Y, trranslation.Z, 0);
                }

            }, () => { });

            glControl.MouseWheel += (s, e) =>
            {
                //going forward/backward current camera direction
                StopTracking();
                center += GetCameraDirection() * e.Delta * ForwardSpeed;
            };

            glControl.MouseDown += GlControl_MouseDown;
            glControl.Paint += GlControl_Paint;
            glControl.SizeChanged += GlControl_SizeChanged;
        }

        private Vector4 GetCameraDirection()
        {
            float dirX = (float)(Math.Cos(yAngle) * Math.Sin(xzAngle));
            float dirY = (float)(Math.Sin(yAngle));
            float dirZ = (float)(Math.Cos(yAngle) * Math.Cos(xzAngle));
            return new Vector4(dirX, dirY, dirZ, 0);
        }

        private Matrix4 GetProjectionMatrix()
        {
            Matrix4 view = Matrix4.LookAt(
                center.Xyz,
                (center+GetCameraDirection()).Xyz,
                Vector3.UnitY
            );

            Matrix4 proj = Matrix4.CreatePerspectiveFieldOfView(
                MathHelper.DegreesToRadians(60f),
                glControl.Width / (float)glControl.Height,
                0.1f,
                5000f
            );

            Matrix4 matrix = view * proj;
            return matrix;
        }

        private void FollowTrackedParticle()
        {
            if (TrackedIdx.HasValue)
            {
                var tracked = computeProgram.GetTrackedParticle();
                var cameraPosition = tracked.position - GetCameraDirection() * app.simulation.followDistance; //move camera back of tracked particle
                var delta = cameraPosition - center;
                var translate = delta * app.simulation.cameraFollowSpeed;
                center += translate;

            }
        }

        public void ResetOrigin()
        {
            center = new Vector4(app.simulation.config.width / 2, app.simulation.config.height / 2, app.simulation.config.depth / 2, 1.0f);
            xzAngle = 0;
            yAngle = 0;
        }

        private void GlControl_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                lock(app.simulation)
                {
                    computeProgram.DownloadData(app.simulation.particles);
                    double minDistance = app.simulation.config.width * 10;
                    int closestIdx = 0;
                    var projectionMatrix = GetProjectionMatrix();


                    for (int idx = 0; idx< app.simulation.particles.Length; idx++)
                    {
                        for (int x = -2; x <= 2; x++)
                            for (int y = -2; y <= 2; y++)
                                for (int z = -2; z <= 2; z++)
                                {
                                    var particlePosition = app.simulation.particles[idx].position;
                                    particlePosition.X += x * app.simulation.config.width;
                                    particlePosition.Y += y * app.simulation.config.height;
                                    particlePosition.Z += z * app.simulation.config.depth;

                                    var screen = GpuUtil.World3DToScreen(particlePosition.Xyz, projectionMatrix, glControl.Width, glControl.Height);
                                    if (screen.HasValue)
                                    {
                                        var distance = Math.Sqrt((screen.Value.X - e.X) * (screen.Value.X - e.X) +
                                                                 (screen.Value.Y - e.Y) * (screen.Value.Y - e.Y));
                                        if (distance < minDistance)
                                        {
                                            minDistance = distance;
                                            closestIdx = idx;
                                        }
                                    }
                                }
                    }

                    if (minDistance < 10)
                    {
                        if (TrackedIdx == closestIdx)
                            StopTracking();
                        else
                            StartTracking(closestIdx);
                    }
                }
            }
        }

        public void UploadParticleData()
        {
            computeProgram.UploadData(app.simulation.particles);
        }

        public void StartTracking(int idx)
        {
            TrackedIdx = idx;
            app.simulation.config.trackedIdx = TrackedIdx ?? -1;
            computeProgram.Run(app.simulation.config, app.simulation.forces);
        }

        public void StopTracking()
        {
            if (TrackedIdx != null)
            {
                TrackedIdx = null;
                app.simulation.config.trackedIdx = TrackedIdx ?? -1;
                computeProgram.Run(app.simulation.config, app.simulation.forces);
            }
        }

        private void GlControl_SizeChanged(object? sender, EventArgs e)
        {
            if (glControl.Width <= 0 || glControl.Height <= 0)
                return;

            if (!glControl.Context.IsCurrent)
                glControl.MakeCurrent();

            GL.Viewport(0, 0, glControl.Width, glControl.Height);
            glControl.Invalidate();
        }

        private void GlControl_Paint(object? sender, PaintEventArgs e)
        {
            try
            {
                lock (app.simulation)
                {
                    FollowTrackedParticle();
                    displayProgram.Run(GetProjectionMatrix(), app.simulation.config.particleCount, app.simulation.particleSize, new Vector2(glControl.Width, glControl.Height));
                    glControl.SwapBuffers();
                    frameCounter++;
                }

                Capture();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public void Step()
        {
            if (Application.Current.MainWindow == null || Application.Current.MainWindow.WindowState == System.Windows.WindowState.Minimized)
                return;

            //compute
            if (!Paused)
            {
                lock (app.simulation)
                {
                    app.simulation.config.trackedIdx = TrackedIdx ?? -1;
                    computeProgram.Run(app.simulation.config, app.simulation.forces);
                }
            }

            glControl.Invalidate();
        }

        private void Capture()
        {
            //combine PNGs into video:
            //mp4: ffmpeg -f image2 -framerate 60 -i rec1/frame_%05d.png -vf "scale=trunc(iw/2)*2:trunc(ih/2)*2" -r 60 -vcodec libx264 -pix_fmt yuv420p out.mp4 -y
            //gif: ffmpeg -framerate 60 -ss2 -i rec/frame_%05d.png -vf "select='not(mod(n,2))',setpts=N/FRAME_RATE/TB" -t 5 -r 20 simple2.gif
            //reduce bitrate:  ffmpeg -i in.mp4 -c:v libx264 -b:v 4236000 -pass 2 -c:a aac -b:a 128k out.mp4
            var recDir = app.configWindow.recordDir?.ToString();
            if (!recFrameNr.HasValue && !string.IsNullOrWhiteSpace(recDir))
            {
                recFrameNr = 0;
            }

            if (recFrameNr.HasValue && string.IsNullOrWhiteSpace(recDir))
                recFrameNr = null;

            if (recFrameNr.HasValue && !string.IsNullOrWhiteSpace(recDir))
            {
                string recFilename = $"{recDir}\\frame_{recFrameNr.Value.ToString("00000")}.png";
                glControl.MakeCurrent();
                int width = glControl.Width;
                int height = glControl.Height;
                int bufferSize = width * height * 4;
                if (captureBuffer == null || bufferSize != captureBuffer.Length)
                    captureBuffer = new byte[bufferSize];
                GL.ReadPixels(
                    0, 0,
                    width, height,
                    OpenTK.Graphics.OpenGL.PixelFormat.Bgra,
                    PixelType.UnsignedByte,
                    captureBuffer
                );

                TextureUtil.SaveBufferToFile(captureBuffer, width, height, recFilename);
                recFrameNr = recFrameNr.Value + 1;
            }
        }
    }
}
