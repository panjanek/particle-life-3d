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

        private int quadVao;

        private int quadVbo;

        private int quadEbo;


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

            float[] quad =
                {
                    -1, -1,
                     1, -1,
                     1,  1,
                    -1,  1
                };

            uint[] indices = { 0, 1, 2, 2, 3, 0 };

            quadVao = GL.GenVertexArray();
            quadVbo = GL.GenBuffer();
            quadEbo = GL.GenBuffer();

            GL.BindVertexArray(quadVao);

            GL.BindBuffer(BufferTarget.ArrayBuffer, quadVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, quad.Length * sizeof(float), quad, BufferUsageHint.StaticDraw);

            GL.VertexAttribPointer(5, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
            GL.EnableVertexAttribArray(5);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, quadEbo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

            GL.BindVertexArray(0);

            /* original
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);
            GL.DepthMask(true);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(
                BlendingFactor.SrcAlpha,
                BlendingFactor.OneMinusSrcAlpha
            );
            */

            /* working
            
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);
            GL.DepthMask(false);   // IMPORTANT: do NOT write depth

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(
                BlendingFactor.SrcAlpha,
                BlendingFactor.One    // additive blending
            );

            GL.Disable(EnableCap.DepthTest);
            */

            GL.Disable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);
            GL.DepthMask(false);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha,
                         BlendingFactor.One);

        }

        public void Run(Matrix4 projectionMatrix, int particlesCount, float particleSize, Vector2 viewportSize, Matrix4 view, List<Vector4> torusOffsets, Vector4 trackedPos)
        {
            GL.Clear(
                ClearBufferMask.ColorBufferBit |
                ClearBufferMask.DepthBufferBit
            );
            foreach (var torusOffset in torusOffsets)
            {
                GL.UseProgram(program);
                GL.BindVertexArray(quadVao);

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
