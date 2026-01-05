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

        private static int[] particleIndices;

        public static void Log(string message)
        {
            File.AppendAllText(LogFile, $"{message}\n");
        }

        public static void DebugSolver(bool bufferB, ShaderConfig config, SolverProgram solver)
        {
            if (particles == null || particles.Length != config.particleCount)
            {
                particles = new Particle[config.particleCount];
                particleIndices = new int[config.particleCount];
            }

            solver.DownloadParticles(particles, bufferB);
            var counts = solver.cellCounts;
            var offsets = solver.cellOffsets;
            solver.DownloadIntBuffer(particleIndices, solver.particleIndicesBuffer, config.particleCount);

            var cellSize = config.cellSize;

            List<int>[] expected = new List<int>[config.totalCellCount];
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
                    if (expected[p.cellIndex] == null)
                        expected[p.cellIndex] = new List<int>();
                    expected[p.cellIndex].Add(idx);
                }
                else
                {
                    throw new Exception("bad cell");
                }
            }

            for(int cellIdx=0; cellIdx < config.totalCellCount; cellIdx++)
            {
                var expectedList = expected[cellIdx].OrderBy(x => x).ToArray();
                var computed = particleIndices.Skip(offsets[cellIdx]).Take(counts[cellIdx]).OrderBy(x => x).ToArray();
                if (expectedList.Length != computed.Length)
                    throw new Exception("invalid counts");

                for (int i = 0; i < computed.Length; i++)
                    if (expectedList[i] != computed[i])
                        throw new Exception($"difference at {i} for {cellIdx}");
            }
            Console.WriteLine("seems ok");


        }
    }
}
