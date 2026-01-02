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

            // create dummy vao
            GL.GenVertexArrays(1, out dummyVao);
            GL.BindVertexArray(dummyVao);
        }

        public void Run(Matrix4 projectionMatrix, int particlesCount, float particleSize)
        {
            GL.Enable(EnableCap.ProgramPointSize);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
            GL.BlendEquation(OpenTK.Graphics.OpenGL.BlendEquationMode.FuncAdd);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.UseProgram(program);
            GL.BindVertexArray(dummyVao);
            GL.UseProgram(program);
            GL.UniformMatrix4(projLocation, false, ref projectionMatrix);
            GL.Uniform1(particleSizeLocation, particleSize);
            GL.DrawArrays(PrimitiveType.Points, 0, particlesCount * 9);
        }
    }
}
