using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Slime3D.Models
{
    [StructLayout(LayoutKind.Explicit, Size = 72)]
    public unsafe struct ShaderConfig
    {
        public ShaderConfig()
        {

        }

        [FieldOffset(0)] public int particleCount = 0;

        [FieldOffset(4)] public float dt = 0.025f;

        [FieldOffset(8)] public float sensorDist = 15f;

        [FieldOffset(12)] public float t = 0;

        [FieldOffset(16)] public float randomization = 0.75f;

        [FieldOffset(20)] public float fieldSize = 800;

        [FieldOffset(24)] public float cellSize = 0;

        [FieldOffset(28)] public float maxDist = 60;

        [FieldOffset(32)] public int speciesCount = 0;

        [FieldOffset(36)] public float velocity = 15f;

        [FieldOffset(40)] public int trackedIdx;

        [FieldOffset(44)] public float maxForce = 15;

        [FieldOffset(48)] public float torque_k = 0.3f;

        [FieldOffset(52)] public int cellCount = 0;

        [FieldOffset(56)] public int totalCellCount = 0;

        [FieldOffset(60)] public float maxSteer = 3f;

        [FieldOffset(64)] public float flow = -0.1f;

        [FieldOffset(68)] public float attraction = -0.1f;

        [FieldOffset(72)] public float freeThreshold = 1f;
    }
}
