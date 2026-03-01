using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.GLControl;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using ParticleLife3D.Utils;

namespace ParticleLife3D.Gpu
{
    public class DisplayProgram
    {
        private int program;

        private int projLocation;

        private int particleSizeLocation;

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
            viewLocation = GL.GetUniformLocation(program, "view");
            if (viewLocation == -1) throw new Exception("Uniform 'view' not found. Shader optimized it out?");
            torusOffsetLocation = GL.GetUniformLocation(program, "torusOffset");
            if (torusOffsetLocation == -1) throw new Exception("Uniform 'torusOffset' not found. Shader optimized it out?");

            dummyVao = GL.GenVertexArray();
            GL.BindVertexArray(0);
        }

        public void Run(Matrix4 projectionMatrix, int particlesCount, float particleSize, Vector2 viewportSize, Matrix4 view, List<Vector4> torusOffsets, Vector4 trackedPos)
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
                GL.Uniform1(particleSizeLocation, particleSize);
                GL.UniformMatrix4(viewLocation, false, ref view);
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
