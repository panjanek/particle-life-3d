using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using ParticleLife3D.Models;
using ParticleLife3D.Utils;

namespace ParticleLife3D.Gpu
{
    public class SolverProgram
    {
        private int maxGroupsX;

        private int solvingProgram;

        private int tilingCountProgram;

        private int tilingBinProgram;

        private int uboConfig;

        private int forcesBuffer;

        private int pointsBufferA;

        private int pointsBufferB;

        private int trackingBuffer;

        public int cellCountBuffer;

        public int cellOffsetBuffer;
        public int cellOffsetBuffer2;

        public int particleIndicesBuffer;

        private int currentParticlesCount;

        private int currentTotalCellsCount;

        private int shaderPointStrideSize;

        public int[] cellCounts;

        public int[] cellOffsets;

        private Particle trackedParticle;

        public SolverProgram()
        {
            uboConfig = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.UniformBuffer, uboConfig);
            int configSizeInBytes = Marshal.SizeOf<ShaderConfig>();
            GL.BufferData(BufferTarget.UniformBuffer, configSizeInBytes, IntPtr.Zero, BufferUsageHint.StaticDraw);
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, uboConfig);

            //constant-length buffers
            CreateBuffer(ref forcesBuffer, Simulation.MaxSpeciesCount * Simulation.MaxSpeciesCount * Simulation.KeypointsCount, Marshal.SizeOf<Vector4>());
            CreateBuffer(ref trackingBuffer, 1, Marshal.SizeOf<Particle>());

            GL.GetInteger((OpenTK.Graphics.OpenGL.GetIndexedPName)All.MaxComputeWorkGroupCount, 0, out maxGroupsX);
            shaderPointStrideSize = Marshal.SizeOf<Particle>();
            solvingProgram = ShaderUtil.CompileAndLinkComputeShader("solver.comp");
            tilingCountProgram = ShaderUtil.CompileAndLinkComputeShader("tiling_count.comp");
            tilingBinProgram = ShaderUtil.CompileAndLinkComputeShader("tiling_bin.comp");
        }

        public void Run(ref ShaderConfig config, Vector4[] forces)
        {
            PrepareBuffers(config.particleCount, config.totalCellCount);
            int dispatchGroupsX = (currentParticlesCount + ShaderUtil.LocalSizeX - 1) / ShaderUtil.LocalSizeX;
            if (dispatchGroupsX > maxGroupsX)
                dispatchGroupsX = maxGroupsX;           

            config.cellCount = (int)Math.Floor(config.fieldSize / config.maxDist);
            config.cellSize = config.fieldSize / config.cellCount;
            config.totalCellCount = config.cellCount * config.cellCount * config.cellCount;
            PrepareBuffers(config.particleCount, config.totalCellCount);

            //upload config
            GL.BindBuffer(BufferTarget.UniformBuffer, uboConfig);
            GL.BufferData(BufferTarget.UniformBuffer, Marshal.SizeOf<ShaderConfig>(), ref config, BufferUsageHint.StaticDraw);

            // ------------------------ run tiling ---------------------------
            //count
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, cellCountBuffer);
            GL.ClearBufferData(BufferTarget.ShaderStorageBuffer, PixelInternalFormat.R32ui, PixelFormat.RedInteger, PixelType.UnsignedInt, IntPtr.Zero);
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, uboConfig);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, pointsBufferA);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 6, cellCountBuffer);
            GL.UseProgram(tilingCountProgram);
            GL.DispatchCompute(dispatchGroupsX, 1, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit | MemoryBarrierFlags.ShaderImageAccessBarrierBit);

            //offset
            DownloadIntBuffer(cellCounts, cellCountBuffer, currentTotalCellsCount);
            int sum = 0;
            for(int c=0; c<currentTotalCellsCount; c++)
            {
                cellOffsets[c] = sum;
                sum += cellCounts[c];
            }

            //fill
            UploadIntBuffer(cellOffsets, cellOffsetBuffer, currentTotalCellsCount);
            GL.CopyNamedBufferSubData(cellOffsetBuffer, cellOffsetBuffer2, IntPtr.Zero, IntPtr.Zero, currentTotalCellsCount * sizeof(uint));
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, uboConfig);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, pointsBufferA);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 7, cellOffsetBuffer);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 8, particleIndicesBuffer);

            GL.UseProgram(tilingBinProgram);
            GL.DispatchCompute(dispatchGroupsX, 1, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit | MemoryBarrierFlags.ShaderImageAccessBarrierBit);

            /*
            var offsetsBroken = new int[currentTotalCellsCount];
            DownloadIntBuffer(offsetsBroken, cellOffsetBuffer, currentTotalCellsCount);
            var offsetsOk = new int[currentTotalCellsCount];
            DownloadIntBuffer(offsetsOk, cellOffsetBuffer2, currentTotalCellsCount);
            */
            //DebugUtil.DebugSolver(false, config, this);

            // ------------------------ run solver --------------------------
            //upload forces
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, forcesBuffer);
            GL.BufferSubData(BufferTarget.ShaderStorageBuffer, 0, Marshal.SizeOf<Vector4>() * Simulation.MaxSpeciesCount * Simulation.MaxSpeciesCount * Simulation.KeypointsCount, forces);

            //bind ubo and buffers
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, uboConfig);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, pointsBufferA);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 2, pointsBufferB);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 4, forcesBuffer);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 5, trackingBuffer);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 6, cellCountBuffer);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 7, cellOffsetBuffer2);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 8, particleIndicesBuffer);

            GL.UseProgram(solvingProgram);
            GL.DispatchCompute(dispatchGroupsX, 1, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit | MemoryBarrierFlags.ShaderImageAccessBarrierBit);

            (pointsBufferA, pointsBufferB) = (pointsBufferB, pointsBufferA);
        }

        public void UploadParticles(Particle[] particles)
        {
            PrepareBuffers(particles.Length, currentTotalCellsCount);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, pointsBufferA);
            GL.BufferSubData(BufferTarget.ShaderStorageBuffer, 0, particles.Length * shaderPointStrideSize, particles);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, pointsBufferB);
            GL.BufferSubData(BufferTarget.ShaderStorageBuffer, 0, particles.Length * shaderPointStrideSize, particles);
        }

        public void DownloadParticles(Particle[] particles, bool bufferB = false)
        {
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, bufferB ? pointsBufferB : pointsBufferA);

            GL.GetBufferSubData(
                BufferTarget.ShaderStorageBuffer,
                IntPtr.Zero,
                particles.Length * Marshal.SizeOf<Particle>(),
                particles
            );

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
        }

        public Particle GetTrackedParticle()
        {
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, trackingBuffer);

            GL.GetBufferSubData(
                BufferTarget.ShaderStorageBuffer,
                IntPtr.Zero,
                Marshal.SizeOf<Particle>(),
                ref trackedParticle
            );

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
            return trackedParticle;
        }

        public void DownloadIntBuffer(int[] buffer, int bufferId, int size)
        {
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, bufferId);

            GL.GetBufferSubData(
                BufferTarget.ShaderStorageBuffer,
                IntPtr.Zero,
                size * Marshal.SizeOf<int>(),
                buffer
            );

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
        }

        public void UploadIntBuffer(int[] buffer, int bufferId, int size)
        {
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, bufferId);
            GL.BufferSubData(BufferTarget.ShaderStorageBuffer, 0, size * Marshal.SizeOf<int>(), buffer);
        }

        private void PrepareBuffers(int particlesCount, int totalCellsCount)
        {
            if (currentParticlesCount != particlesCount)
            {
                currentParticlesCount = particlesCount;
                CreateBuffer(ref pointsBufferA, currentParticlesCount, shaderPointStrideSize);
                CreateBuffer(ref pointsBufferB, currentParticlesCount, shaderPointStrideSize);
                CreateBuffer(ref particleIndicesBuffer, currentParticlesCount, Marshal.SizeOf<int>());
            }

            if (currentTotalCellsCount != totalCellsCount)
            {
                currentTotalCellsCount = totalCellsCount;
                CreateBuffer(ref cellCountBuffer, currentTotalCellsCount, Marshal.SizeOf<int>());
                CreateBuffer(ref cellOffsetBuffer, currentTotalCellsCount, Marshal.SizeOf<int>());
                CreateBuffer(ref cellOffsetBuffer2, currentTotalCellsCount, Marshal.SizeOf<int>());
                cellCounts = new int[totalCellsCount];
                cellOffsets = new int[totalCellsCount];
            }
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
