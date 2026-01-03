using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
