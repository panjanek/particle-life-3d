using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ParticleLife3D.Gpu;
using ParticleLife3D.Models;

namespace ParticleLife3D.Utils
{
    public class DebugUtil
    {
        public static bool Debug = true;

        public static string LogFile = "log.txt";

        public static void Log(string message)
        {
            File.AppendAllText(LogFile, $"{message}\n");
        }

        public static void DebugSolver(Simulation sim, SolverProgram solver)
        {
            solver.DownloadParticles(sim.particles, true);
            var cellIndices = solver.DownloadCellIndices();
            var parts = sim.particles;
            var cellSize = sim.config.cellSize;
            for(int idx = 0; idx<sim.config.particleCount; idx++)
            {
                var p = parts[idx];
                var gridX = p.cellIndex % sim.config.cellCount;
                var gridY = (p.cellIndex / sim.config.cellCount) % sim.config.cellCount;
                var gridZ = p.cellIndex / (sim.config.cellCount * sim.config.cellCount);
                
                if (p.position.X >= gridX * cellSize && p.position.X <= (gridX + 1) * cellSize &&
                    p.position.Y >= gridY * cellSize && p.position.Y <= (gridY + 1) * cellSize &&
                    p.position.Z >= gridZ * cellSize && p.position.Z <= (gridZ + 1) * cellSize)
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
