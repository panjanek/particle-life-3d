using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Mathematics;

namespace ParticleLife3D.Utils
{
    public static class MathUtil
    {
        public static double GetTorusDistance(double d1, double d2, double size)
        {
            double d = d2 - d1;
            if (Math.Abs(d) > size / 2)
            {
                d = d - size * Math.Sign(d);
            }

            return d;
        }

        public static double Amplify(double x, int pow)
        {
            double a = 1;
            for (int i = 0; i < pow; i++)
                a = a * (1 - x);

            return 1 - a;

        }

        public static float TorusCorrection(float x, float size)
        {
            if (x < 0)
                x += size;
            else if (x > size)
                x -= size;
            return x;
        }

        public static Vector4 TorusCorrection(Vector4 pos, float size)
        {
            return new Vector4(TorusCorrection(pos.X, size), TorusCorrection(pos.Y, size), TorusCorrection(pos.Z, size), pos.W);
        }
    }
}
