using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ParticleLife3D.Models
{
    [StructLayout(LayoutKind.Explicit, Size = 56)]
    public struct ShaderConfig
    {
        public ShaderConfig()
        {

        }

        [FieldOffset(0)] public int particleCount = 0;

        [FieldOffset(4)] public float dt = 0.1f;

        [FieldOffset(8)] public float sigma2 = 0f;

        [FieldOffset(12)] public float clampVel = 0;

        [FieldOffset(16)] public float clampAcc = 0;

        [FieldOffset(20)] public float width = 1920;

        [FieldOffset(24)] public float height = 1080;

        [FieldOffset(28)] public float maxDist = 100;

        [FieldOffset(32)] public int speciesCount = 0;

        [FieldOffset(36)] public float damping = 0.1f;

        [FieldOffset(40)] public int trackedIdx;

        [FieldOffset(44)] public float maxForce = 15;

        [FieldOffset(48)] public float amp = 1f;

        [FieldOffset(52)] public float depth = 1000f;
    }
}
