using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ParticleLife3D.Models
{
    public static class SimFactory
    {
        private static readonly JsonSerializerOptions serializerOptions = new JsonSerializerOptions() { IncludeFields = true, WriteIndented = true };
        public static void SaveToFile(Simulation recipe, string fn)
        {
            var str = JsonSerializer.Serialize(recipe, serializerOptions);
            File.WriteAllText(fn, str);
        }

        public static Simulation LoadFromFile(string fn)
        {
            var str = File.ReadAllText(fn);
            var sim = JsonSerializer.Deserialize<Simulation>(str, serializerOptions);
            sim.InitializeParticles(sim.config.particleCount);
            return sim;
        }
    }
}
