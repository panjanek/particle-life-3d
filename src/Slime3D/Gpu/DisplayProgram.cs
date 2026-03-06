using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.GLControl;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using Slime3D.Utils;

namespace Slime3D.Gpu
{
    public class DisplayProgram
    {
        private int program;

        private int projLocation;

        private int particleSizeLocation;

        private int softnessLocation;

        private int fogDensityLocation;

        private int viewLocation;

        private int torusOffsetLocation;

        private int dummyVao;

        public DisplayProgram()
        {
            program = ShaderUtil.CompileAndLinkRenderShader("display.vert", "display.frag");
            projLocation = GL.GetUniformLocation(program, "projection");
            if (projLocation == -1) throw new Exception("Uniform 'projection' not found. Shader optimized it out?");
            particleSizeLocation = GL.GetUniformLocation(program, "paricleSize");
            if (particleSizeLocation == -1) throw new Exception("Uniform 'paricleSize' not found. Shader optimized it out?");
            softnessLocation = GL.GetUniformLocation(program, "softness");
            if (softnessLocation == -1) throw new Exception("Uniform 'softness' not found. Shader optimized it out?");
            fogDensityLocation = GL.GetUniformLocation(program, "fogDensity");
            if (fogDensityLocation == -1) throw new Exception("Uniform 'fogDensity' not found. Shader optimized it out?");
            viewLocation = GL.GetUniformLocation(program, "view");
            if (viewLocation == -1) throw new Exception("Uniform 'view' not found. Shader optimized it out?");
            torusOffsetLocation = GL.GetUniformLocation(program, "torusOffset");
            if (torusOffsetLocation == -1) throw new Exception("Uniform 'torusOffset' not found. Shader optimized it out?");

            dummyVao = GL.GenVertexArray();
            GL.BindVertexArray(0);
        }

        public void Run(Matrix4 projectionMatrix,
                       Matrix4 viewMatrix,
                       int particlesCount, 
                       float particleSize, 
                       float softness,
                       float fogDensity,
                       List<Vector4> torusOffsets)
        {
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);
            GL.DepthMask(true);
            GL.Clear(
                ClearBufferMask.ColorBufferBit |
                ClearBufferMask.DepthBufferBit
            );
            GL.DepthMask(false);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha,
                         BlendingFactor.One);

            foreach (var torusOffset in torusOffsets)
            {
                GL.UseProgram(program);
                GL.BindVertexArray(dummyVao);

                GL.UniformMatrix4(projLocation, false, ref projectionMatrix);
                GL.UniformMatrix4(viewLocation, false, ref viewMatrix);
                GL.Uniform1(particleSizeLocation, particleSize);
                GL.Uniform1(softnessLocation, softness);
                GL.Uniform1(fogDensityLocation, fogDensity);
                var offset = torusOffset;
                GL.Uniform4(torusOffsetLocation, ref offset);

                GL.DrawArrays(
                    PrimitiveType.Points,
                    0,
                    particlesCount
                );
            }
        }
    }
}
