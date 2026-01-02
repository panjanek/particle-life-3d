using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL;

namespace ParticleLife3D.Gpu
{
    public static class ShaderUtil
    {
        // 16, 32, 64, 128, 256 - depending on GPU architecture/vendor. Can be set as first commandline parameter
        public static int LocalSizeX = 64;
        public static int CompileAndLinkComputeShader(string compFile)
        {
            // Compile compute shader
            string source = LoadShaderCode(compFile);
            source = source.Replace("{LocalSizeX}", LocalSizeX.ToString());
            int computeShader = GL.CreateShader(ShaderType.ComputeShader);
            GL.ShaderSource(computeShader, source);
            GL.CompileShader(computeShader);
            GL.GetShader(computeShader, ShaderParameter.CompileStatus, out int status);
            if (status != (int)All.True)
            {
                var log = GL.GetShaderInfoLog(computeShader);
                throw new Exception(log);
            }

            int program = GL.CreateProgram();
            GL.AttachShader(program, computeShader);
            GL.LinkProgram(program);
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out status);
            if (status != (int)All.True)
            {
                throw new Exception(GL.GetProgramInfoLog(program));
            }

            return program;
        }

        public static int CompileAndLinkRenderShader(string vertFile, string fragFile)
        {
            string vertexSource = LoadShaderCode(vertFile);
            string fragmentSource = LoadShaderCode(fragFile);

            // Compile vertex shader
            int vertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertexShader, vertexSource);
            GL.CompileShader(vertexShader);

            GL.GetShader(vertexShader, ShaderParameter.CompileStatus, out int vStatus);
            if (vStatus != (int)All.True)
            {
                string log = GL.GetShaderInfoLog(vertexShader);
                throw new Exception("Vertex shader compilation failed:\n" + log);
            }

            // Compile fragment shader
            int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragmentShader, fragmentSource);
            GL.CompileShader(fragmentShader);

            GL.GetShader(fragmentShader, ShaderParameter.CompileStatus, out int fStatus);
            if (fStatus != (int)All.True)
            {
                string log = GL.GetShaderInfoLog(fragmentShader);
                throw new Exception("Fragment shader compilation failed:\n" + log);
            }

            // Create program and link
            int program = GL.CreateProgram();
            GL.AttachShader(program, vertexShader);
            GL.AttachShader(program, fragmentShader);

            GL.LinkProgram(program);

            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int linkStatus);
            if (linkStatus != (int)All.True)
            {
                string log = GL.GetProgramInfoLog(program);
                throw new Exception("Shader program linking failed:\n" + log);
            }

            // Shaders can be detached and deleted after linking
            GL.DetachShader(program, vertexShader);
            GL.DetachShader(program, fragmentShader);
            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);

            return program;
        }

        public static string LoadShaderCode(string name)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var a = assembly.GetManifestResourceNames();
            var resourceName = $"ParticleLife3D.shaders.{name}";
            using Stream stream = assembly.GetManifestResourceStream(resourceName) ?? throw new InvalidOperationException($"Resource not found: {resourceName}");
            using StreamReader reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
