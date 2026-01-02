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
        public const double ZoomingSpeed = 0.0005;
        public int FrameCounter => frameCounter;

        public bool Paused { get; set; }

        public int? TrackedIdx { get; set; }

        private Panel placeholder;

        private System.Windows.Forms.Integration.WindowsFormsHost host;

        private GLControl glControl;

        private int frameCounter;

        private ComputeProgram computeProgram;

        private DisplayProgram displayProgram;

        private float cameraDistance = 2000f;

        private Vector4 center;

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
            glControl.Paint += GlControl_Paint;
            glControl.SizeChanged += GlControl_SizeChanged;

            //setup required features
            GL.Enable(EnableCap.ProgramPointSize);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
            GL.BlendEquation(OpenTK.Graphics.OpenGL.BlendEquationMode.FuncAdd);
            GL.Enable(EnableCap.PointSprite);

            computeProgram = new ComputeProgram();
            displayProgram = new DisplayProgram();
            UploadParticleData();

            cameraDistance = app.simulation.config.depth;
            center = new Vector4(app.simulation.config.width / 2, app.simulation.config.height / 2, 0, 1.0f);

            var dragging = new DraggingHandler(glControl, (mousePos, isLeft) => isLeft, (prev, curr) =>
            {
                StopTracking();
                var delta = (curr - prev);
                delta.Y = -delta.Y;
                center -= new Vector4(delta.X, delta.Y, 0, 0);

            }, () => { });

            glControl.MouseWheel += (s, e) =>
            {
                center.Z += (float)(e.Delta * 1);
            };

            glControl.MouseDown += GlControl_MouseDown;
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
                        var particlePosition = app.simulation.particles[idx].position;
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

        private Matrix4 GetProjectionMatrix()
        {
            Matrix4 view = Matrix4.LookAt(
                new Vector3(center.X, center.Y, center.Z + cameraDistance),
                new Vector3(center.X, center.Y, 0),
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

        private Matrix4 GetProjectionMatrix2()
        {
            Matrix4 view = Matrix4.LookAt(
                new Vector3(center.X, center.Y, center.Z + cameraDistance),
                new Vector3(center.X, center.Y, 0),
                Vector3.UnitY
);

            Matrix4 proj = Matrix4.CreatePerspectiveFieldOfView(
                MathHelper.DegreesToRadians(60f),
                glControl.Width / (float)glControl.Height,
                0.1f,
                5000f
            );

            Matrix4 matrix = proj * view;
            return matrix;
        }

        private void FollowTrackedParticle()
        {
            if (TrackedIdx.HasValue)
            {
                var projectionMatrix = GetProjectionMatrix();
                var tracked = computeProgram.GetTrackedParticle();
                var trackedScreenPosition = tracked.position;
                var delta = trackedScreenPosition - center;
                
                var move = delta * app.simulation.cameraFollowSpeed;

                if (Math.Abs(delta.X) > 0.75* app.simulation.config.width)
                {
                    move.X = (float)Math.Sign(delta.X) * app.simulation.config.width;
                }

                if (Math.Abs(delta.Y) > 0.75 * app.simulation.config.height)
                {
                    move.Y = (float)Math.Sign(delta.Y) * app.simulation.config.height;
                }

                center += move;
            }
        }

        private void GlControl_Paint(object? sender, PaintEventArgs e)
        {
            lock (app.simulation)
            {
                //FollowTrackedParticle();
                displayProgram.Run(GetProjectionMatrix(), app.simulation.config.particleCount, app.simulation.particleSize);
                glControl.SwapBuffers();
                frameCounter++;
            }

            Capture();
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
