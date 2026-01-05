using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL;
using ParticleLife3D.Gpu;
using ParticleLife3D.Models;

namespace ParticleLife3D.Utils
{
    public class DebugUtil
    {
        public static bool Debug = true;

        public static string LogFile = "log.txt";

        private static Particle[] particles;

        public static void Log(string message)
        {
            File.AppendAllText(LogFile, $"{message}\n");
        }

        public static void DebugSolver(bool bufferB, ShaderConfig config, SolverProgram solver)
        {
            if (particles == null || particles.Length != config.particleCount)
                particles = new Particle[config.particleCount];
            solver.DownloadParticles(particles, bufferB);
            var cellIndices = solver.DownloadIntBuffer(solver.cellIndicesBuffer, config.particleCount);
            var particleIndices = solver.DownloadIntBuffer(solver.radixProgram.valsA, config.particleCount);
            var cellSize = config.cellSize;
            for(int idx = 0; idx<config.particleCount; idx++)
            {
                var p = particles[idx];
                var gridX = p.cellIndex % config.cellCount;
                var gridY = (p.cellIndex / config.cellCount) % config.cellCount;
                var gridZ = p.cellIndex / (config.cellCount * config.cellCount);
                
                if (p.position.X >= gridX * cellSize && p.position.X < (gridX + 1) * cellSize &&
                    p.position.Y >= gridY * cellSize && p.position.Y < (gridY + 1) * cellSize &&
                    p.position.Z >= gridZ * cellSize && p.position.Z < (gridZ + 1) * cellSize)
                {

                }
                else
                {
                    throw new Exception("bad cell");
                }

                if (p.cellIndex != cellIndices[idx])
                    throw new Exception("bad index");
            }



        }
    }
}
