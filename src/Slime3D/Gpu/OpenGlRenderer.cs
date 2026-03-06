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
using Slime3D.Models;
using Slime3D.Utils;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;
using AppContext = Slime3D.Models.AppContext;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using Models_AppContext = Slime3D.Models.AppContext;
using Panel = System.Windows.Controls.Panel;
using PixelFormat = OpenTK.Graphics.OpenGL.PixelFormat;

namespace Slime3D.Gpu
{
    public class OpenGlRenderer
    {
        public const float ForwardSpeed = 0.1f;

        public const float DirectionChangeSpeed = 0.003f;

        public int FrameCounter => frameCounter;

        public bool Paused { get; set; }

        public int? TrackedIdx { get; set; }

        private Panel placeholder;

        private System.Windows.Forms.Integration.WindowsFormsHost host;

        private GLControl glControl;

        private int frameCounter;

        private SolverProgram solverProgram;

        private DisplayProgram displayProgram;

        private Vector4 center;

        private double xzAngle = 0;

        private double yAngle = 0;

        private Models_AppContext app;

        public byte[] captureBuffer;

        private int? recFrameNr;

        public OpenGlRenderer(Panel placeholder, Models_AppContext app)
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
                APIVersion = new Version(4, 3), 
                Profile = ContextProfile.Core,
                Flags = ContextFlags.Default,
                IsEventDriven = false,
                DepthBits = 24
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

            solverProgram = new SolverProgram();
            displayProgram = new DisplayProgram();
            UploadParticleData();
            ResetOrigin();

            var dragging = new DraggingHandler(glControl, (mousePos, btn) => true, (prev, curr, btn) =>
            {
                var delta = (curr - prev);
                if (btn == MouseButtons.Right)
                {
                    // change camera angle
                    xzAngle -= (delta.X) * DirectionChangeSpeed;
                    yAngle -= (delta.Y) * DirectionChangeSpeed;
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
                    center = MathUtil.TorusCorrection(center, app.simulation.config.fieldSize);
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
                    center = MathUtil.TorusCorrection(center, app.simulation.config.fieldSize);
                }
            };

            glControl.MouseDoubleClick += GlControl_MouseDoubleClick;
            glControl.Paint += GlControl_Paint;
            glControl.SizeChanged += GlControl_SizeChanged;
            GlControl_SizeChanged(this, null);
        }

        private void GlControl_MouseDoubleClick(object? sender, MouseEventArgs e)
        {
            lock (app.simulation)
            {
                //DebugUtil.DebugSolver(true, app.simulation.config, solverProgram);
                solverProgram.DownloadParticles(app.simulation.particles);
                int pixelRadius = 5;
                int? selectedIdx = null;
                float minDepth = app.simulation.config.fieldSize * 10;
                var projectionMatrix = GetCombinedProjectionMatrix();
                for (int idx = 0; idx < app.simulation.particles.Length; idx++)
                {
                    for (int x = -1; x <= 1; x++)
                        for (int y = -1; y <= 1; y++)
                            for (int z = -1; z <= 1; z++)
                            {
                                var particlePosition = app.simulation.particles[idx].position;
                                particlePosition.X += x * app.simulation.config.fieldSize;
                                particlePosition.Y += y * app.simulation.config.fieldSize;
                                particlePosition.Z += z * app.simulation.config.fieldSize;

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
                xzAngle += 0.002;

                var tracked = solverProgram.GetTrackedParticle();
                var cameraPosition = tracked.position - GetCameraDirection() * app.simulation.followDistance; //move camera to back of tracked particle
                var delta = cameraPosition - center;
                var translate = delta;
                var newCenter = center + translate;
                center = newCenter;
                //do not correct torus then tracking not to interfere with fade. tracked.position will be torus corrected anyway
            }
            else
            {
                var cameraDir = GetCameraDirection();
                cameraDir.Normalize();
                center += app.simulation.forwardMove * cameraDir;
                center = MathUtil.TorusCorrection(center, app.simulation.config.fieldSize);
            }
            
        }

        public void ResetOrigin()
        {
            StopTracking();
            center = new Vector4(app.simulation.config.fieldSize / 2, app.simulation.config.fieldSize / 2, app.simulation.config.fieldSize / 2, 1.0f);
            xzAngle = 0;
            yAngle = 0;
        }

        public void UploadParticleData() => solverProgram.UploadParticles(app.simulation.particles);
     
        public void StartTracking(int idx)
        {
            TrackedIdx = idx;
            app.simulation.config.trackedIdx = TrackedIdx ?? -1;
            solverProgram.Run(ref app.simulation.config, app.simulation.forces);
        }

        public void StopTracking()
        {
            if (TrackedIdx != null)
            {
                TrackedIdx = null;
                app.simulation.config.trackedIdx = TrackedIdx ?? -1;
                solverProgram.Run(ref app.simulation.config, app.simulation.forces);
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
            FollowTrackedParticle();
            var torusOffsets = GetVisibleTorusOffsets();
            displayProgram.Run(
                GetProjectionMatrix(),
                GetViewMatrix(),
                app.simulation.config.particleCount,
                app.simulation.particleSize,
                app.simulation.particleSoftness,
                app.simulation.fogDensity,
                torusOffsets);
            glControl.SwapBuffers();
            frameCounter++;
            Capture();
        }

        private List<Vector4> GetVisibleTorusOffsets()
        {
            float S = app.simulation.config.fieldSize;
            float radius = 0.5f * MathF.Sqrt(3 * S * S);
            Vector3 localCenter = new Vector3(S, S, S) * 0.5f;
            Vector3 camPos = center.Xyz;
            Vector3 camDir = GetCameraDirection().Xyz;

            var repeats = app.simulation.torusRepeats;
            List<Vector4> torusOffsets = new List<Vector4>();
            for (int tx = -repeats; tx <= repeats; tx++)
                for (int ty = -repeats; ty <= repeats; ty++)
                    for (int tz = -repeats; tz <= repeats; tz++)
                    {
                        var torusOffset = new Vector4(tx * S, ty * S, tz * S, 0);
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
                app.simulation.config.trackedIdx = TrackedIdx ?? -1;
                app.simulation.config.t += app.simulation.config.dt;
                solverProgram.Run(ref app.simulation.config, app.simulation.forces);
            }

            var recDir = app.configWindow.recordDir?.ToString();
            if (string.IsNullOrWhiteSpace(recDir))
            {
                glControl.Invalidate();
            }
            else
            {
                GL.Finish();
                GlControl_Paint(null, null);
            }
        }

        private void Capture()
        {
            //combine PNGs into video:
            //mp4: ffmpeg -f image2 -framerate 60 -i rec/frame_%05d.png -r 60 -vcodec libx264 -preset veryslow -crf 12 -profile:v high -pix_fmt yuv420p out.mp4 -y
            //gif: ffmpeg -framerate 60 -ss 2 -i rec/frame_%05d.png -vf "select='not(mod(n,2))',setpts=N/FRAME_RATE/TB" -t 5 -r 20 simple2.gif
            //cut: ffmpeg -ss 35 -i move-full.mp4 -t 35 -c copy chase-1.mp4
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
