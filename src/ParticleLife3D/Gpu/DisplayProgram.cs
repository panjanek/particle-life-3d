using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.GLControl;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace ParticleLife3D.Gpu
{
    public class DisplayProgram
    {
        private int program;

        private int projLocation;

        private int viewportSizeLocation;

        private int particleSizeLocation;

        private int dummyVao;

        public DisplayProgram()
        {
            program = ShaderUtil.CompileAndLinkRenderShader("display.vert", "display.frag");
            projLocation = GL.GetUniformLocation(program, "projection");
            if (projLocation == -1) throw new Exception("Uniform 'projection' not found. Shader optimized it out?");
            particleSizeLocation = GL.GetUniformLocation(program, "paricleSize");
            if (particleSizeLocation == -1) throw new Exception("Uniform 'paricleSize' not found. Shader optimized it out?");
            viewportSizeLocation = GL.GetUniformLocation(program, "viewportSize");
            if (viewportSizeLocation == -1) throw new Exception("Uniform 'viewportSize' not found. Shader optimized it out?");

            // create dummy vao
            GL.GenVertexArrays(1, out dummyVao);
            GL.BindVertexArray(dummyVao);
        }

        public void Run(Matrix4 projectionMatrix, int particlesCount, float particleSize, Vector2 viewportSize)
        {
            GL.Enable(EnableCap.ProgramPointSize);

            // depth
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);
            GL.DepthMask(true);

            // blending
            //GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            // --- CLEAR BOTH ---
            GL.Clear(
                ClearBufferMask.ColorBufferBit |
                ClearBufferMask.DepthBufferBit
            );


            GL.BindVertexArray(dummyVao);
            GL.UseProgram(program);
            GL.UniformMatrix4(projLocation, false, ref projectionMatrix);
            GL.Uniform1(particleSizeLocation, particleSize);
            GL.Uniform2(viewportSizeLocation, viewportSize);
            GL.DrawArrays(PrimitiveType.Points, 0, particlesCount * 27);
        }
    }
}
