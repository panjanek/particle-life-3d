using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
