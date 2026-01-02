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

namespace ParticleLife3D.Gpu
{
    public class ComputeProgram
    {
        private int maxGroupsX;

        private int program;

        private int uboConfig;

        private int forcesBuffer;

        private int pointsBufferA;

        private int pointsBufferB;

        private int pointsTorusBuffer;

        private int trackingBuffer;

        private int pointsCount;

        private int shaderPointStrideSize;

        private Particle trackedParticle;

        public ComputeProgram()
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
            program = ShaderUtil.CompileAndLinkComputeShader("solver.comp");
        }

        public void Run(ShaderConfig config, Vector4[] forces)
        {
            PrepareBuffer(config.particleCount);

            //upload config
            GL.BindBuffer(BufferTarget.UniformBuffer, uboConfig);
            GL.BufferData(BufferTarget.UniformBuffer, Marshal.SizeOf<ShaderConfig>(), ref config, BufferUsageHint.StaticDraw);
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, uboConfig);

            //upload forces
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, forcesBuffer);
            GL.BufferSubData(BufferTarget.ShaderStorageBuffer, 0, Marshal.SizeOf<Vector4>() * Simulation.MaxSpeciesCount * Simulation.MaxSpeciesCount * Simulation.KeypointsCount, forces);

            //bind storage buffers
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, pointsBufferA);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 2, pointsBufferB);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 3, pointsTorusBuffer);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 4, forcesBuffer);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 5, trackingBuffer);

            GL.UseProgram(program);
            int dispatchGroupsX = (pointsCount + ShaderUtil.LocalSizeX - 1) / ShaderUtil.LocalSizeX;
            if (dispatchGroupsX > maxGroupsX)
                dispatchGroupsX = maxGroupsX;
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

                //torus buffer
                if (pointsTorusBuffer > 0)
                {
                    GL.DeleteBuffer(pointsTorusBuffer);
                    pointsTorusBuffer = 0;
                }
                GL.GenBuffers(1, out pointsTorusBuffer);
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, pointsTorusBuffer);
                GL.BufferData(BufferTarget.ShaderStorageBuffer, 9 * pointsCount * shaderPointStrideSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);
             }
        }
    }
}
