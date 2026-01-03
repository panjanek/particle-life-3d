using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.GLControl;
using OpenTK.Mathematics;

namespace ParticleLife3D.Utils
{
    public class DraggingHandler
    {
        private bool isDragging;

        private Vector2? previousPoint;

        private Func<Vector2, MouseButtons, bool> canDrag;

        private Action<Vector2, Vector2, MouseButtons> dragging;

        private Action stop;

        private GLControl glControl;

        public DraggingHandler(GLControl glControl, Func<Vector2, MouseButtons, bool> canDrag, Action<Vector2, Vector2, MouseButtons> dragging, Action stop)
        {
            this.canDrag = canDrag;
            this.dragging = dragging;
            this.stop = stop;
            this.glControl = glControl;
            glControl.MouseDown += GlControl_MouseDown;
            glControl.MouseMove += GlControl_MouseMove;
            glControl.MouseUp += GlControl_MouseUp;
            this.stop = stop;
        }

        private void GlControl_MouseUp(object? sender, MouseEventArgs e)
        {
            isDragging = false;
            previousPoint = null;
            if (stop != null)
                stop();
        }

        private void GlControl_MouseMove(object? sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                var current = e.Location;
                if (dragging != null && previousPoint.HasValue)
                    dragging(previousPoint.Value, PositionToVector(current), e.Button);
                previousPoint = PositionToVector(current);
            }
        }

        private void GlControl_MouseDown(object? sender, MouseEventArgs e)
        {
            if (canDrag == null || canDrag(PositionToVector(e.Location), e.Button))
            {
                isDragging = true;
                previousPoint = PositionToVector(e.Location);
            }
        }

        private Vector2 PositionToVector(Point point)
        {
            return new Vector2(point.X, point.Y);
        }
    }
}
