using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParticleLife3D.Gpu
{
    public class RadixProgram
    {
        private int histogramProgram;
        private int prefixsumProgram;
        private int scatterProgram;


        int keysA, keysB;            // ping-pong cellIndex keys
        int valsA, valsB;            // ping-pong particle indices
        int histogram;               // 16 uints
        int offsets;                 // 16 uints
        int cellStart;               // cellCountTotal uints
        int cellCount;               // cellCountTotal uints


        public RadixProgram()
        {
            histogramProgram = ShaderUtil.CompileAndLinkComputeShader("radix_histogram.comp");
            prefixsumProgram = ShaderUtil.CompileAndLinkComputeShader("radix_prefixsum.comp");
            scatterProgram = ShaderUtil.CompileAndLinkComputeShader("radix_scatter.comp");
        }

        private void CreateBuffer(ref int bufferId, int elementCount, int elementSize)
        {
            if (bufferId > 0)
            {
                GL.DeleteBuffer(bufferId);
                bufferId = 0;
            }
            GL.GenBuffers(1, out bufferId);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, bufferId);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, elementCount * elementSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);
        }
    }
}
