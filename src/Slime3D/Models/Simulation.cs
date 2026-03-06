using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Security.RightsManagement;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using OpenTK.Audio.OpenAL;
using OpenTK.Mathematics;

namespace Slime3D.Models
{
    public class Simulation
    {
        public const int MaxSpeciesCount = 6;

        public const int KeypointsCount = 3;

        public ShaderConfig config;

        public float particleSize = 3000f;

        public float particleSoftness = 3.0f;

        public float fogDensity = 0.0005f;

        public float forwardMove = 0.15f;

        public int torusRepeats = 4;

        [JsonIgnore]
        public Particle[] particles;

        [JsonIgnore]
        public Vector4[] forces;

        public int seed = 11;

        public float followDistance = 75; 

        //this is for json serialization
        public float[][] F
        {
            get
            {
                var res = new float[forces.Length][];
                for (int i = 0; i < forces.Length; i++)
                    res[i] = [forces[i].X, forces[i].Y];
                return res;
            }
            set
            {
                for (int i = 0; i < forces.Length; i++)
                    forces[i] = new Vector4(value[i][0], value[i][1], 0, 0);
            }
        }

        public Simulation()
        {
            config = new ShaderConfig();
            forces = new Vector4[MaxSpeciesCount * MaxSpeciesCount * KeypointsCount];
        }

        public void StartSimulation(int particlesCount, int speciesCount, float size)
        {
            var previousSpeciesCount = config.speciesCount;
            config.speciesCount = speciesCount;
            config.fieldSize = size;
            config.particleCount = particlesCount;
            InitializeParticles(particlesCount);
            var rnd = new Random(seed);
            InitializeForces();
            /*
            if (speciesCount > previousSpeciesCount)
            {
                for(int i = previousSpeciesCount; i< speciesCount; i++)
                {
                    for (int j = 0; j < speciesCount; j++)
                    {
                        InitialOneForce(i, j, rnd);
                    }
                }
            }*/
        }

        public static int GetForceOffset(int specMe, int specOther)
        {
            int offset = (specMe * MaxSpeciesCount + specOther) * KeypointsCount;
            return offset;

        }

        private void SetSimpleForce(int specMe, int specOther, float val0, float val1)
        {
            int offset = GetForceOffset(specMe, specOther);
            var d = config.maxDist / KeypointsCount;
            forces[offset + 0] = new Vector4(0 * d, val0, 0, 0);
            forces[offset + 1] = new Vector4(1 * d, 0, 0, 0);
            forces[offset + 2] = new Vector4(2 * d, val1, 0, 0);
        }

        public void InitializeForces()
        {
            var rnd = new Random(seed); //4
            for (int i = 0; i < config.speciesCount; i++)
            {
                for (int j = 0; j < config.speciesCount; j++)
                {
                    InitialOneForce(i, j, rnd);
                }
            }
        }

        public void InitialOneForce(int i, int j, Random rnd)
        {
            float m = config.maxForce;
            if (i == j)
                SetSimpleForce(i, j, 0.5f * m, 0);
            else if (i < j)
                SetSimpleForce(i, j, -0.5f * m, 0);
            else 
                SetSimpleForce(i, j, 0.1f * m, 0);
        }

        public void InitializeParticles(int count)
        {
            if (particles == null || particles.Length != count)
                particles = new Particle[count];

            var rnd = new Random(1);
            for(int i=0; i< count; i++)
            {
                particles[i].position = new Vector4(config.fieldSize * rnd.NextSingle(), config.fieldSize * rnd.NextSingle(), config.fieldSize * rnd.NextSingle(), 0);
                particles[i].species = rnd.Next(config.speciesCount);

                var dir = new Vector4(rnd.NextSingle() * 2 - 1, rnd.NextSingle() * 2 - 1, rnd.NextSingle() * 2 - 1, 0);
                dir.Normalize();
                particles[i].direction = dir;

                /*
                var center = new Vector4(config.fieldSize / 2, config.fieldSize / 2, config.fieldSize / 2, 0);
                var radius = config.fieldSize / 2;
                while ((particles[i].position - center).Length > radius)
                    particles[i].position = new Vector4(config.fieldSize * rnd.NextSingle(), config.fieldSize * rnd.NextSingle(), config.fieldSize * rnd.NextSingle(), 0);


                var inward = center - particles[i].position;
                inward.Normalize();
                particles[i].direction = inward;*/
            }
        }
    }
}
