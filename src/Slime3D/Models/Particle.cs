using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Mathematics;

namespace Slime3D.Models
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Particle
    {
        public Vector4 position; // xyz = position
        public Vector4 velocity; // xyz = velocity
        public int species;
        public int flags;
        public int cellIndex;
        public float xzAngle;
        public float yAngle;
        public int _pad0;
        public int _pad1;
        public int _pad2;
        public Vector4 direction;
    }
}
