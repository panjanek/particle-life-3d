using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL;
using PixelFormat = OpenTK.Graphics.OpenGL.PixelFormat;

namespace ParticleLife3D.Gpu
{
    public static class TextureUtil
    {
        public static int CreateRgba32fTexture(int width, int height)
        {
            int plotTex;
            GL.GenTextures(1, out plotTex);
            GL.BindTexture(TextureTarget.Texture2D, plotTex);

            GL.TexImage2D(
                TextureTarget.Texture2D,
                0,
                PixelInternalFormat.Rgba32f, // accumulation-safe  //R32ui //Rgba32f
                width,
                height,
                0,
                PixelFormat.Rgba,
                PixelType.Float,
                IntPtr.Zero
            );

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            return plotTex;
        }

        public static int CreateIntegerTexture(int width, int height)
        {
            int plotTex;
            GL.GenTextures(1, out plotTex);
            GL.BindTexture(TextureTarget.Texture2D, plotTex);

            GL.TexImage2D(
                TextureTarget.Texture2D,
                0,
                PixelInternalFormat.R32ui,
                width,
                height,
                0,
                PixelFormat.RedInteger,   // IMPORTANT
                PixelType.UnsignedInt,
                IntPtr.Zero
            );

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMinFilter.Nearest);

            return plotTex;
        }

        public static void SaveBufferToFile(byte[] pixels, int width, int height, string fileName)
        {
            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i + 3] = 255;   // force A = 255 for BGRA
            }

            using (Bitmap bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                var data = bmp.LockBits(
                    new Rectangle(0, 0, width, height),
                    ImageLockMode.WriteOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb
                );

                System.Runtime.InteropServices.Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
                bmp.UnlockBits(data);
                bmp.Save(fileName, ImageFormat.Png);
            }
        }
    }
}
