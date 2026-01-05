using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Compute.OpenCL;
using OpenTK.Graphics.OpenGL;
using ParticleLife3D.Models;

namespace ParticleLife3D.Gpu
{
    public class RadixProgram
    {
        const int RADIX_BITS = 4;
        const int BUCKETS = 16;
        const int PASSES = 32 / RADIX_BITS;

        private int histogramProgram;
        private int prefixsumProgram;
        private int scatterProgram;

        private int keysA;
        private int keysB;
        public int valsA;
        private int valsB;
        private int histogram;               // 16 uints
        private int offsets;                 // 16 uints

        private int currentParticlesCount;

        private int histNumElementsLoc;
        private int histShiftLoc;

        private int scatterNumElementsLoc;
        private int scatterShiftLoc;

        private int maxGroupsX;


        public RadixProgram()
        {
            GL.GetInteger((OpenTK.Graphics.OpenGL.GetIndexedPName)All.MaxComputeWorkGroupCount, 0, out maxGroupsX);

            prefixsumProgram = ShaderUtil.CompileAndLinkComputeShader("radix_prefixsum.comp");
            

            histogramProgram = ShaderUtil.CompileAndLinkComputeShader("radix_histogram.comp");
            histNumElementsLoc = GL.GetUniformLocation(histogramProgram, "numElements");
            if (histNumElementsLoc == -1) throw new Exception("Uniform 'numElements' not found. Shader optimized it out?");
            histShiftLoc = GL.GetUniformLocation(histogramProgram, "shift");
            if (histShiftLoc == -1) throw new Exception("Uniform 'shift' not found. Shader optimized it out?");

            scatterProgram = ShaderUtil.CompileAndLinkComputeShader("radix_scatter.comp");
            scatterNumElementsLoc = GL.GetUniformLocation(scatterProgram, "numElements");
            if (scatterNumElementsLoc == -1) throw new Exception("Uniform 'numElements' not found. Shader optimized it out?");
            scatterShiftLoc = GL.GetUniformLocation(scatterProgram, "shift");
            if (scatterShiftLoc == -1) throw new Exception("Uniform 'shift' not found. Shader optimized it out?");

            CreateBuffer(ref histogram, 16, Marshal.SizeOf<int>());
            CreateBuffer(ref offsets, 16, Marshal.SizeOf<int>());
        }

        public void Run(ShaderConfig config, int cellIndicesBuffer)
        {
            int dispatchGroupsX = (currentParticlesCount + ShaderUtil.LocalSizeX - 1) / ShaderUtil.LocalSizeX;
            if (dispatchGroupsX > maxGroupsX)
                dispatchGroupsX = maxGroupsX;

            GL.CopyNamedBufferSubData(cellIndicesBuffer, keysA, IntPtr.Zero, IntPtr.Zero, currentParticlesCount * sizeof(uint));
            GL.MemoryBarrier(MemoryBarrierFlags.BufferUpdateBarrierBit);

            int inKeys = keysA, outKeys = keysB;
            int inVals = valsA, outVals = valsB;

            for (int pass = 0; pass < PASSES; pass++)
            {
                int shift = pass * RADIX_BITS;

                // ---- 1) Clear histogram ----
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, histogram);
                GL.ClearBufferData(BufferTarget.ShaderStorageBuffer,
                                   PixelInternalFormat.R32ui,
                                   PixelFormat.RedInteger,
                                   PixelType.UnsignedInt,
                                   IntPtr.Zero);

                // ---- 2) Histogram pass ----
                GL.UseProgram(histogramProgram);


                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 10, inKeys);
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 14, histogram);

                var testInKeys = DownloadIntBuffer(inKeys, currentParticlesCount);
                var testinVals = DownloadIntBuffer(inVals, currentParticlesCount);

                GL.Uniform1(histNumElementsLoc, (uint)config.particleCount);
                GL.Uniform1(histShiftLoc, (uint)shift);
                GL.DispatchCompute(dispatchGroupsX, 1, 1);
                GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);

                var testHistogram = DownloadIntBuffer(histogram, 16);

                // ---- 3) Prefix sum pass ----
                GL.UseProgram(prefixsumProgram);
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 14, histogram);
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 15, offsets);

                GL.DispatchCompute(1, 1, 1);
                GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);

                var testHistogram2 = DownloadIntBuffer(histogram, 16);
                var testOffsets = DownloadIntBuffer(offsets, 16);

                // ---- 4) Scatter pass ----
                GL.UseProgram(scatterProgram);
                GL.Uniform1(scatterNumElementsLoc, (uint)config.particleCount);
                GL.Uniform1(scatterShiftLoc, (uint)shift);

                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 10, inKeys);
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 11, inVals);
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 12, outKeys);
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 13, outVals);
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 15, offsets);

                GL.DispatchCompute(dispatchGroupsX, 1, 1);
                GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);

                var testOutKeys = DownloadIntBuffer(outKeys, config.particleCount);
                var testOutVals = DownloadIntBuffer(outVals, config.particleCount);

                // ---- swap ping-pong ----
                (inKeys, outKeys) = (outKeys, inKeys);
                (inVals, outVals) = (outVals, inVals);
            }

            var testKeys = DownloadIntBuffer(inKeys, config.particleCount);
            var testVals = DownloadIntBuffer(inVals, config.particleCount);

            var a = 1;
        }

        public void PrepareBuffers(int particlesCount)
        {
            if (currentParticlesCount != particlesCount)
            {
                currentParticlesCount = particlesCount;
                CreateBuffer(ref keysA, currentParticlesCount, Marshal.SizeOf<int>());
                CreateBuffer(ref keysB, currentParticlesCount, Marshal.SizeOf<int>());
                CreateBuffer(ref valsA, currentParticlesCount, Marshal.SizeOf<int>());
                CreateBuffer(ref valsB, currentParticlesCount, Marshal.SizeOf<int>());
            }
        }

        public int[] DownloadIntBuffer(int bufferId, int size)
        {
            var buffer = new int[size];
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, bufferId);

            GL.GetBufferSubData(
                BufferTarget.ShaderStorageBuffer,
                IntPtr.Zero,
                size * Marshal.SizeOf<int>(),
                buffer
            );

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

            return buffer;
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
