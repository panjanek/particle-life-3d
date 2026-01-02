using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Mathematics;

namespace ParticleLife3D.Models
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Particle
    {
        public Vector4 position; // xyz = position
        public Vector4 velocity; // xyz = velocity
        public int species;
        public int flags;
        private int _pad0;
        private int _pad1;
    }
}
