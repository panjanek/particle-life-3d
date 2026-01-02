using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using ParticleLife3D.Models;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace ParticleLife3D.Gui
{
    public class ForceGraph : Canvas
    {
        private const double MinKeypointsDistance = 2;

        private const int HorizondatGridLinesCount = 4;

        private const int SnapDistance = 6;

        public Vector4[] Forces { get; set; }

        public Action Changed { get; set; }

        private Ellipse[] dots;

        private Line[] gridLinesVert;

        private Line[] gridLinesHoriz;

        private Line[] lines;

        private Polygon[] polygons;

        private bool updating;

        private int? dragged;

        private double maxDist;

        private double maxForce;

        private double dotSize = 10;

        public ForceGraph()
        {
            ClipToBounds = true;
            Forces = new Vector4[Simulation.KeypointsCount];
            dots = new Ellipse[Simulation.KeypointsCount];
            lines = new Line[Simulation.KeypointsCount-1];
            gridLinesVert = new Line[Simulation.KeypointsCount];
            Background = Brushes.Black;
            var gridColor = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 48, 48, 48));
            var axisColor = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 96, 96, 96));
            for(int i=0; i<Forces.Length; i++)
            {
                var dot = new Ellipse();
                dot.Stroke = Brushes.Black;
                dot.StrokeThickness = 1;
                dot.Fill = Brushes.Yellow;
                dot.Width = dotSize;
                dot.Height = dotSize;
                dot.Visibility = Visibility.Visible;
                dot.Tag = i.ToString();
                dot.SetValue(Canvas.ZIndexProperty, 1000);
                Children.Add(dot);
                AddDragging(dot);
                dots[i] = dot;
                gridLinesVert[i] = new Line() { StrokeThickness = 1, Stroke = gridColor };
                Children.Add(gridLinesVert[i]);
                if (i < Forces.Length - 1)
                {
                    var line = new Line() { StrokeThickness = 1, Stroke = Brushes.White };
                    Children.Add(line);
                    lines[i] = line;
                }
            }

            gridLinesHoriz = new Line[HorizondatGridLinesCount];
            for (int i=0; i< gridLinesHoriz.Length; i++)
            {
                gridLinesHoriz[i] = new Line() { StrokeThickness = 1, Stroke = (i == gridLinesHoriz.Length/2) ? axisColor : gridColor };
                Children.Add(gridLinesHoriz[i]);
            }

            polygons = new Polygon[(Simulation.KeypointsCount - 1) * 2];
            for(int i=0; i<polygons.Length; i++)
            {
                polygons[i] = new Polygon() { StrokeThickness = 0 };
                Children.Add(polygons[i]);
            }
        }

        private void AddDragging(Ellipse dot)
        {
            MouseLeftButtonUp += (s, e) =>
            {
                dots.ToList().ForEach(d => d.ReleaseMouseCapture());
                dragged = null; 
            };

            dot.MouseLeftButtonDown += (s, e) =>
            {
                var draggedDot = (Ellipse)s;
                var idx = WpfUtil.GetTagAsInt(draggedDot);
                dragged = idx;
                dot.CaptureMouse();
                e.Handled = true;
            };

            MouseMove += (s, e) =>
            {
                var idx = dragged;
                if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed && idx.HasValue)
                {
                    var pos = e.GetPosition(this);
                    for (int i = 0; i < gridLinesHoriz.Length; i++)
                    {
                        for (int j = 0; j < gridLinesVert.Length; j++)
                        {
                            var gridX = i * Width / gridLinesVert.Length;
                            var gridY = j * Height / gridLinesHoriz.Length;
                            if (Math.Abs(pos.X - gridX) < SnapDistance)
                            {
                                pos.X = gridX;
                            }

                            if (Math.Abs(pos.Y - gridY) < SnapDistance)
                                pos.Y = gridY;
                        }
                    }

                    double scaleY = Height / (maxForce * 2);
                    double scaleX = Width / maxDist;
                    var newForceY = ((Height / 2) - pos.Y) / scaleY;
                    var newForceX = pos.X / scaleX;

                    if (idx == 0)
                        newForceX = 0;
                    if (idx == Forces.Length - 1)
                        newForceY = 0;
                    if (idx>0 && newForceX < Forces[idx.Value - 1].X + MinKeypointsDistance)
                            newForceX = Forces[idx.Value - 1].X + MinKeypointsDistance;
                    if (idx < Forces.Length - 1 && newForceX > Forces[idx.Value + 1].X - MinKeypointsDistance)
                        newForceX = Forces[idx.Value + 1].X - MinKeypointsDistance;


                    Forces[idx.Value].X = (float)newForceX;
                    Forces[idx.Value].Y = (float)newForceY;
                    UpdateGraph(Forces, maxDist, maxForce);
                    if (Changed != null)
                        Changed();
                }
            };
        }

        public void UpdateGraph(Vector4[] forces, double maxDist, double maxForce)
        {
            updating = true;
            this.maxDist = maxDist;
            this.maxForce = maxForce;
            Forces = forces.ToArray();
            double scaleX = Width / maxDist;
            double scaleY = Height / (maxForce * 2);
            for(int i=0; i<forces.Length; i++)
            {
                gridLinesVert[i].X1 = (i * Width / Simulation.KeypointsCount);
                gridLinesVert[i].X2 = gridLinesVert[i].X1;
                gridLinesVert[i].Y1 = 0;
                gridLinesVert[i].Y2 = Height;
                dots[i].SetValue(Canvas.LeftProperty, ToPixelX(forces[i].X) - dotSize/2);
                dots[i].SetValue(Canvas.TopProperty, ToPixelY(forces[i].Y) - dotSize/2);
                if (i<lines.Length)
                {
                    lines[i].X1 = ToPixelX(forces[i].X);
                    lines[i].Y1 = ToPixelY(forces[i].Y);
                    lines[i].X2 = ToPixelX(forces[i + 1].X);
                    lines[i].Y2 = ToPixelY(forces[i + 1].Y);
                    DrawPolygons(i);
                }
            }

            for (int i = 0; i < gridLinesHoriz.Length; i++)
            {
                gridLinesHoriz[i].X1 = 0;
                gridLinesHoriz[i].X2 = Width;
                gridLinesHoriz[i].Y1 = i * Height / gridLinesHoriz.Length;
                gridLinesHoriz[i].Y2 = gridLinesHoriz[i].Y1;
            }

            updating = false;
        }

        private void DrawPolygons(int i)
        {
            int offset = polygons.Length / 2;
            var positiveColor = new SolidColorBrush(System.Windows.Media.Color.FromArgb(128, 255, 0, 0));
            var negativeColor = new SolidColorBrush(System.Windows.Media.Color.FromArgb(128, 0, 0, 255));
            if (Forces[i].Y * Forces[i + 1].Y >= 0)
            {
                polygons[i + offset].Visibility = Visibility.Collapsed;
                SetPolygon(i,
                    [
                        new Point(ToPixelX(Forces[i].X), ToPixelY(0)),
                        new Point(ToPixelX(Forces[i].X), ToPixelY(Forces[i].Y)),
                        new Point(ToPixelX(Forces[i+1].X), ToPixelY(Forces[i+1].Y)),
                        new Point(ToPixelX(Forces[i+1].X), ToPixelY(0)),
                ], Forces[i].Y >= 0 && Forces[i + 1].Y >= 0 ? positiveColor : negativeColor);
            }
            else
            {
                var cx = Forces[i].X + (Forces[i + 1].X - Forces[i].X) * (Forces[i].Y / (Forces[i].Y - Forces[i+1].Y));
                SetPolygon(i,
                     [
                        new Point(ToPixelX(Forces[i].X), ToPixelY(0)),
                        new Point(ToPixelX(Forces[i].X), ToPixelY(Forces[i].Y)),
                        new Point(ToPixelX(cx), ToPixelY(0))
                     ], Forces[i].Y < 0 ? negativeColor : positiveColor);

                SetPolygon(i+offset,
                    [
                        new Point(ToPixelX(cx), ToPixelY(0)),
                        new Point(ToPixelX(Forces[i+1].X), ToPixelY(Forces[i+1].Y)),
                        new Point(ToPixelX(Forces[i+1].X), ToPixelY(0))
                    ], Forces[i+1].Y < 0 ? negativeColor : positiveColor);
            }
        }

        private void SetPolygon(int idx, Point[] points, Brush brush)
        {
            polygons[idx].SetValue(Canvas.LeftProperty, 0d);
            polygons[idx].SetValue(Canvas.TopProperty, 0d);
            polygons[idx].Points = new PointCollection(points.Select(p => new Point(p.X, p.Y)));
            polygons[idx].Visibility = Visibility.Visible;
            polygons[idx].Fill = brush;
        }

        private double ToPixelX(double x)
        {
            double scaleX = Width / maxDist;
            return x * scaleX;
        }

        private double ToPixelY(double y)
        {
            double scaleY = Height / (maxForce * 2);
            return Height / 2 - y * scaleY;
        }
    }
}
