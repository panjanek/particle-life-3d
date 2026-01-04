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

        public RadixProgram()
        {
            histogramProgram = ShaderUtil.CompileAndLinkComputeShader("radix_histogram.comp");
            prefixsumProgram = ShaderUtil.CompileAndLinkComputeShader("radix_prefixsum.comp");
            scatterProgram = ShaderUtil.CompileAndLinkComputeShader("radix_scatter.comp");
        }
    }
}
