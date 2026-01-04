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

        private int pointsCount;

        private int shaderPointStrideSize;

        private Particle trackedParticle;

        public SolverProgram()
        {
            uboConfig = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.UniformBuffer, uboConfig);
            int configSizeInBytes = Marshal.SizeOf<ShaderConfig>();
            GL.BufferData(BufferTarget.UniformBuffer, configSizeInBytes, IntPtr.Zero, BufferUsageHint.StaticDraw);
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, uboConfig);

            //forces buffer
            GL.GenBuffers(1, out forcesBuffer);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, forcesBuffer);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, Marshal.SizeOf<Vector4>() * Simulation.MaxSpeciesCount * Simulation.MaxSpeciesCount * Simulation.KeypointsCount, IntPtr.Zero, BufferUsageHint.DynamicDraw);

            //tracking buffer
            GL.GenBuffers(1, out trackingBuffer);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, trackingBuffer);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, Marshal.SizeOf<Particle>(), IntPtr.Zero, BufferUsageHint.DynamicDraw);

            GL.GetInteger((OpenTK.Graphics.OpenGL.GetIndexedPName)All.MaxComputeWorkGroupCount, 0, out maxGroupsX);
            shaderPointStrideSize = Marshal.SizeOf<Particle>();
            solvingProgram = ShaderUtil.CompileAndLinkComputeShader("solver.comp");
            tilingProgram = ShaderUtil.CompileAndLinkComputeShader("tiling.comp");
        }

        public void Run(ShaderConfig config, Vector4[] forces)
        {
            int dispatchGroupsX = (pointsCount + ShaderUtil.LocalSizeX - 1) / ShaderUtil.LocalSizeX;
            if (dispatchGroupsX > maxGroupsX)
                dispatchGroupsX = maxGroupsX;

            PrepareBuffer(config.particleCount);

            //upload config
            GL.BindBuffer(BufferTarget.UniformBuffer, uboConfig);
            GL.BufferData(BufferTarget.UniformBuffer, Marshal.SizeOf<ShaderConfig>(), ref config, BufferUsageHint.StaticDraw);

            // ------------------------ run tiling ---------------------------
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, uboConfig);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, pointsBufferA);
            GL.UseProgram(tilingProgram);
            GL.DispatchCompute(dispatchGroupsX, 1, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit | MemoryBarrierFlags.ShaderImageAccessBarrierBit);

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

            GL.UseProgram(solvingProgram);
            GL.DispatchCompute(dispatchGroupsX, 1, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit | MemoryBarrierFlags.ShaderImageAccessBarrierBit);

            (pointsBufferA, pointsBufferB) = (pointsBufferB, pointsBufferA);
        }

        public void UploadData(Particle[] particles)
        {
            PrepareBuffer(particles.Length);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, pointsBufferA);
            GL.BufferSubData(BufferTarget.ShaderStorageBuffer, 0, particles.Length * shaderPointStrideSize, particles);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, pointsBufferB);
            GL.BufferSubData(BufferTarget.ShaderStorageBuffer, 0, particles.Length * shaderPointStrideSize, particles);
        }

        public void DownloadData(Particle[] particles)
        {
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, pointsBufferA);

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

        private void PrepareBuffer(int size)
        {
            if (pointsCount != size)
            {
                pointsCount = size;

                //buffer A
                if (pointsBufferA > 0)
                {
                    GL.DeleteBuffer(pointsBufferA);
                    pointsBufferA = 0;
                }
                GL.GenBuffers(1, out pointsBufferA);
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, pointsBufferA);
                GL.BufferData(BufferTarget.ShaderStorageBuffer, pointsCount * shaderPointStrideSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);
                
                //buffer B
                if (pointsBufferB > 0)
                {
                    GL.DeleteBuffer(pointsBufferB);
                    pointsBufferB = 0;
                }
                GL.GenBuffers(1, out pointsBufferB);
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, pointsBufferB);
                GL.BufferData(BufferTarget.ShaderStorageBuffer, pointsCount * shaderPointStrideSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);
             }
        }
    }
}
