using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ParticleLife3D.Gpu;
using ParticleLife3D.Gui;

namespace ParticleLife3D.Models
{
    public class AppContext
    {
        public Simulation simulation;

        public MainWindow mainWindow;

        public OpenGlRenderer renderer;

        public ConfigWindow configWindow;
    }
}
