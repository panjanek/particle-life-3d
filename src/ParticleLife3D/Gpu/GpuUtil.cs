using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Mathematics;

namespace ParticleLife3D.Gpu
{
    public static class GpuUtil
    {
        public static Vector2 ProjectToScreen(Vector2 position, Matrix4 projectionMatrix, int viewportWidth, int viewportHeight)
        {
            Vector4 p = new Vector4(position.X, position.Y, 0.0f, 1.0f);
            //Vector4 clip = Vector4.Transform(p, projectionMatrix);
            Vector4 clip = new Vector4(
                p.X * projectionMatrix.M11 + p.Y * projectionMatrix.M21 + p.Z * projectionMatrix.M31 + p.W * projectionMatrix.M41,
                p.X * projectionMatrix.M12 + p.Y * projectionMatrix.M22 + p.Z * projectionMatrix.M32 + p.W * projectionMatrix.M42,
                p.X * projectionMatrix.M13 + p.Y * projectionMatrix.M23 + p.Z * projectionMatrix.M33 + p.W * projectionMatrix.M43,
                p.X * projectionMatrix.M14 + p.Y * projectionMatrix.M24 + p.Z * projectionMatrix.M34 + p.W * projectionMatrix.M44
            );

            if (clip.W == 0.0f)
                return Vector2.Zero;

            Vector3 ndc = clip.Xyz / clip.W;

            float x = (ndc.X * 0.5f + 0.5f) * viewportWidth;
            float y = viewportHeight - (ndc.Y * 0.5f + 0.5f) * viewportHeight;  // vertical flip

            return new Vector2(x, y);
        }

        public static Vector2 ScreenToWorld(Vector2 pixel, Matrix4 projection, int viewportWidth, int viewportHeight)
        {
            float x = (pixel.X / viewportWidth) * 2.0f - 1.0f;
            float y = 1.0f - (pixel.Y / viewportHeight) * 2.0f;  // vertical flip

            Vector4 clip = new Vector4(x, y, 0.0f, 1.0f);

            Matrix4 invProj = projection.Inverted();
            //Vector4 worldH = Vector4.Transform(clip, invProj);
            Vector4 worldH = Multiply(invProj, clip);

            if (worldH.W == 0.0f)
                return Vector2.Zero;

            return worldH.Xy / worldH.W;
        }

        public static Vector4 Multiply(Matrix4 m, Vector4 v)
        {
            return new Vector4(
                m.M11 * v.X + m.M21 * v.Y + m.M31 * v.Z + m.M41 * v.W,
                m.M12 * v.X + m.M22 * v.Y + m.M32 * v.Z + m.M42 * v.W,
                m.M13 * v.X + m.M23 * v.Y + m.M33 * v.Z + m.M43 * v.W,
                m.M14 * v.X + m.M24 * v.Y + m.M34 * v.Z + m.M44 * v.W
            );
        }
    }
}
