using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Resources;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Xps;
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

        public const int TorusRepeats = 3;
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

            var dragging = new DraggingHandler(glControl, (mousePos, btn) => true, (prev, curr, btn) =>
            {
                var delta = (curr - prev);
                if (btn == MouseButtons.Right)
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
                    center = MathUtil.TorusCorrection(center, app.simulation.config.width, app.simulation.config.height, app.simulation.config.depth);
                }

            }, () => { });

            glControl.MouseWheel += (s, e) =>
            {
                var delta = e.Delta * ForwardSpeed;
                if (TrackedIdx.HasValue)
                {
                    //change follow distance
                    app.simulation.followDistance -= delta;
                    if (app.simulation.followDistance < 10)
                        app.simulation.followDistance = 10;
                    app.configWindow.UpdateActiveControls();
                    app.configWindow.UpdatePassiveControls();
                }
                else
                {
                    //going forward/backward current camera direction
                    center += GetCameraDirection() * delta;
                    center = MathUtil.TorusCorrection(center, app.simulation.config.width, app.simulation.config.height, app.simulation.config.depth);
                }
            };

            glControl.MouseDoubleClick += GlControl_MouseDoubleClick;
            glControl.Paint += GlControl_Paint;
            glControl.SizeChanged += GlControl_SizeChanged;
        }

        private void GlControl_MouseDoubleClick(object? sender, MouseEventArgs e)
        {
            lock (app.simulation)
            {
                computeProgram.DownloadData(app.simulation.particles);
                int pixelRadius = 5;
                int? selectedIdx = null;
                float minDepth = app.simulation.config.depth * 10;
                var projectionMatrix = GetCombinedProjectionMatrix();
                for (int idx = 0; idx < app.simulation.particles.Length; idx++)
                {
                    for (int x = -1; x <= 1; x++)
                        for (int y = -1; y <= 1; y++)
                            for (int z = -1; z <= 1; z++)
                            {
                                var particlePosition = app.simulation.particles[idx].position;
                                particlePosition.X += x * app.simulation.config.width;
                                particlePosition.Y += y * app.simulation.config.height;
                                particlePosition.Z += z * app.simulation.config.depth;

                                var screenAndDepth = GpuUtil.World3DToScreenWithDepth(particlePosition.Xyz, projectionMatrix, glControl.Width, glControl.Height);
                                if (screenAndDepth.HasValue)
                                {
                                    var screen = screenAndDepth.Value.screen;
                                    var depth = screenAndDepth.Value.depth;
                                    var distance = Math.Sqrt((screen.X - e.X) * (screen.X - e.X) +
                                                             (screen.Y - e.Y) * (screen.Y - e.Y));
                                    if (distance < pixelRadius && depth < minDepth)
                                    {
                                        selectedIdx = idx;
                                        minDepth = depth;
                                    }
                                   
                                }
                            }
                }

                if (selectedIdx.HasValue)
                {
                    if (TrackedIdx == selectedIdx.Value)
                        StopTracking();
                    else
                        StartTracking(selectedIdx.Value);
                }
            }
        }

        private Vector4 GetCameraDirection()
        {
            float dirX = (float)(Math.Cos(yAngle) * Math.Sin(xzAngle));
            float dirY = (float)(Math.Sin(yAngle));
            float dirZ = (float)(Math.Cos(yAngle) * Math.Cos(xzAngle));
            return new Vector4(dirX, dirY, dirZ, 0);
        }

        private Matrix4 GetViewMatrix()
        {
            Matrix4 view = Matrix4.LookAt(
                center.Xyz,
                (center + GetCameraDirection()).Xyz,
                Vector3.UnitY
            );

            return view;
        }

        private Matrix4 GetProjectionMatrix()
        {
            Matrix4 proj = Matrix4.CreatePerspectiveFieldOfView(
                MathHelper.DegreesToRadians(60f),
                glControl.Width / (float)glControl.Height,
                0.1f,
                5000f
            );

            return proj;
        }

        private Matrix4 GetCombinedProjectionMatrix()
        {
            var view = GetViewMatrix();
            var proj = GetProjectionMatrix();
            Matrix4 matrix = view * proj;
            return matrix;
        }

        private void FollowTrackedParticle()
        {
            if (TrackedIdx.HasValue)
            {
                var tracked = computeProgram.GetTrackedParticle();
                var cameraPosition = tracked.position - GetCameraDirection() * app.simulation.followDistance; //move camera to back of tracked particle
                var delta = cameraPosition - center;
                var translate = delta * app.simulation.cameraFollowSpeed;
                center += translate;
                //do not correct torus then tracking not to interfere with fade. tracked.position will be torus corrected anyway
            }
        }

        public void ResetOrigin()
        {
            StopTracking();
            center = new Vector4(app.simulation.config.width / 2, app.simulation.config.height / 2, app.simulation.config.depth / 2, 1.0f);
            xzAngle = 0;
            yAngle = 0;
        }

        public void UploadParticleData() => computeProgram.UploadData(app.simulation.particles);
     
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
            lock (app.simulation)
            {
                FollowTrackedParticle();
                var torusOffsets = GetVisibleTorusOffsets();
                var trackedPos = TrackedIdx.HasValue ? computeProgram.GetTrackedParticle().position : new Vector4(-1000000, 0, 0, 0);
                displayProgram.Run(GetProjectionMatrix(),
                    app.simulation.config.particleCount,
                    app.simulation.particleSize,
                    new Vector2(glControl.Width, glControl.Height),
                    GetViewMatrix(),
                    torusOffsets,
                    trackedPos);

                glControl.SwapBuffers();
                frameCounter++;
            }

            Capture();
        }

        private List<Vector4> GetVisibleTorusOffsets()
        {
            float W = app.simulation.config.width;
            float H = app.simulation.config.height;
            float D = app.simulation.config.depth;
            float radius = 0.5f * MathF.Sqrt(W * W + H * H + D * D);
            Vector3 localCenter = new Vector3(W, H, D) * 0.5f;
            Vector3 camPos = center.Xyz;
            Vector3 camDir = GetCameraDirection().Xyz;

            List<Vector4> torusOffsets = new List<Vector4>();
            for (int tx = -TorusRepeats; tx <= TorusRepeats; tx++)
                for (int ty = -TorusRepeats; ty <= TorusRepeats; ty++)
                    for (int tz = -TorusRepeats; tz <= TorusRepeats; tz++)
                    {
                        var torusOffset = new Vector4(tx * W, ty * H, tz * D, 0);
                        Vector3 repeatCenter = localCenter + torusOffset.Xyz;
                        Vector3 toRepeat = repeatCenter - camPos;
                        float forward = Vector3.Dot(toRepeat, camDir);
                        if (forward < -radius)
                            continue;

                        torusOffsets.Add(torusOffset);
                    }

            return torusOffsets;
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
