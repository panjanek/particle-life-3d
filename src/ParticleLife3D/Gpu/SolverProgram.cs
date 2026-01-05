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

        private int tilingProgram;

        private int uboConfig;

        private int forcesBuffer;

        private int pointsBufferA;

        private int pointsBufferB;

        private int trackingBuffer;

        public int cellIndicesBuffer;

        private int cellStartBuffer;               // cellCountTotal uints

        private int cellCountBuffer;               // cellCountTotal uints

        private int currentParticlesCount;

        private int currentTotalCellsCount;

        private int shaderPointStrideSize;

        public RadixProgram radixProgram;

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
            tilingProgram = ShaderUtil.CompileAndLinkComputeShader("tiling.comp");

            radixProgram = new RadixProgram();
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

           
            radixProgram.PrepareBuffers(config.particleCount);

            //upload config
            GL.BindBuffer(BufferTarget.UniformBuffer, uboConfig);
            GL.BufferData(BufferTarget.UniformBuffer, Marshal.SizeOf<ShaderConfig>(), ref config, BufferUsageHint.StaticDraw);

            // ------------------------ run tiling ---------------------------
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, uboConfig);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, pointsBufferA);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 6, cellIndicesBuffer);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 7, radixProgram.valsA);
            GL.UseProgram(tilingProgram);
            GL.DispatchCompute(dispatchGroupsX, 1, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit | MemoryBarrierFlags.ShaderImageAccessBarrierBit);
            DebugUtil.DebugSolver(false, config, this);
            // ------------------------ run sorting and grouping -------------
            radixProgram.Run(config, cellIndicesBuffer);

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
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 10, cellIndicesBuffer);

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

        private void PrepareBuffers(int particlesCount, int totalCellsCount)
        {
            if (currentParticlesCount != particlesCount)
            {
                currentParticlesCount = particlesCount;
                CreateBuffer(ref pointsBufferA, currentParticlesCount, shaderPointStrideSize);
                CreateBuffer(ref pointsBufferB, currentParticlesCount, shaderPointStrideSize);
                CreateBuffer(ref cellIndicesBuffer, currentParticlesCount, Marshal.SizeOf<int>());
            }

            if (currentTotalCellsCount != totalCellsCount)
            {
                currentTotalCellsCount = particlesCount;
                CreateBuffer(ref cellStartBuffer, currentTotalCellsCount, Marshal.SizeOf<int>());
                CreateBuffer(ref cellCountBuffer, currentTotalCellsCount, Marshal.SizeOf<int>());
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
