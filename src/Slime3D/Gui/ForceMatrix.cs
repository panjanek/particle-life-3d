using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using OpenTK.Mathematics;
using Slime3D.Models;
using Slime3D.Utils;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace Slime3D.Gui
{
    public class ForceMatrix : Canvas
    {
        private Rectangle[,] rectangles;

        private Brush[] DotBrushes = [Brushes.Green, Brushes.Blue, Brushes.Magenta, Brushes.Red , Brushes.Yellow, Brushes.Cyan, Brushes.White, Brushes.Gray];

        private Color[] DotColors = [Colors.Green, Colors.Blue, Colors.Magenta, Colors.Red, Colors.Yellow, Colors.Magenta, Colors.Cyan , Colors.White, Colors.Gray];

        public int SelectedX { get; set; }

        public int SelectedY { get; set; }

        public Action SelectionChanged { get; set; }

        private int speciesCount { get; set; }

        private Ellipse[] verticalDots;

        private Ellipse[] horizontalDots;

        public ForceMatrix()
            :base()
        {
            rectangles = new Rectangle[Simulation.MaxSpeciesCount, Simulation.MaxSpeciesCount];
            verticalDots = new Ellipse[Simulation.MaxSpeciesCount];
            horizontalDots = new Ellipse[Simulation.MaxSpeciesCount];
            SelectedX = 0;
            SelectedY = 0;
            Loaded += ForceMatrix_Loaded;
        }

        private void ForceMatrix_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            Background = Brushes.White;
            var rectSize = Width / Simulation.MaxSpeciesCount;
            for (int x = 0; x < Simulation.MaxSpeciesCount; x++)
            {
                verticalDots[x] = new System.Windows.Shapes.Ellipse() { Fill = DotBrushes[x%DotBrushes.Length], Width = rectSize * 0.75, Height = rectSize * 0.75, Stroke = Brushes.Black, StrokeThickness = 1 };
                verticalDots[x].SetValue(Canvas.LeftProperty, -rectSize);
                verticalDots[x].SetValue(Canvas.TopProperty, x * rectSize);
                verticalDots[x].Tag = x.ToString();
                verticalDots[x].MouseDown += Dot_MouseDown;
                Children.Add(verticalDots[x]);
                horizontalDots[x] = new System.Windows.Shapes.Ellipse() { Fill = DotBrushes[x % DotBrushes.Length], Width = rectSize * 0.75, Height = rectSize * 0.75, Stroke = Brushes.Black, StrokeThickness = 1 };
                horizontalDots[x].SetValue(Canvas.LeftProperty, rectSize*x);
                horizontalDots[x].SetValue(Canvas.TopProperty, - rectSize);
                horizontalDots[x].MouseDown += Dot_MouseDown;
                horizontalDots[x].Tag = x.ToString();
                Children.Add(horizontalDots[x]);
                for (int y = 0; y < Simulation.MaxSpeciesCount; y++)
                {
                    var rect = new Rectangle();
                    rect.SetValue(Canvas.LeftProperty, x * rectSize);
                    rect.SetValue(Canvas.TopProperty, y * rectSize);
                    rect.Stroke = Brushes.Black;
                    rect.StrokeThickness = 1;
                    rect.Fill = Brushes.White;
                    rect.Width = rectSize;
                    rect.Height = rectSize;
                    rect.Visibility = System.Windows.Visibility.Visible;
                    rect.Tag = $"{x},{y}";
                    rect.MouseDown += (s, e) =>
                    {
                        var tag = WpfUtil.GetTagAsString(s);
                        var split = tag.Split(',');
                        var newX = int.Parse(split[0]);
                        var newY = int.Parse(split[1]);
                        if (newX < speciesCount && newY < speciesCount)
                        {
                            SelectedX = newX;
                            SelectedY = newY;
                            UpdateSelection();
                            if (SelectionChanged != null)
                                SelectionChanged();
                        }
                    };

                    Children.Add(rect);
                    rectangles[x, y] = rect;
                }
            }

            UpdateSelection();
            UpdateDots();
        }

        private void Dot_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var dot = (Ellipse)sender;
            var i = WpfUtil.GetTagAsInt(dot);
            UpdateDots();
            if (SelectionChanged != null)
                SelectionChanged();
        }

        public void UpdateSelection()
        {
            for (int x = 0; x < Simulation.MaxSpeciesCount; x++)
            {
                for (int y = 0; y < Simulation.MaxSpeciesCount; y++)
                {
                    var rect = rectangles[x, y];
                    if (x == SelectedX && y == SelectedY)
                    {
                        rect.Stroke = Brushes.LightGreen;
                        rect.StrokeThickness = 2;
                    }
                    else
                    {
                        rect.Stroke = Brushes.Black;
                        rect.StrokeThickness = 1;
                    }
                }
            }
        }

        public void UpdateDots()
        {
            for(int i=0; i<Simulation.MaxSpeciesCount; i++)
            {
                var mainBrush = DotBrushes[i % DotBrushes.Length];
                var mainColor = DotColors[i % DotColors.Length];
                var res = mainBrush;

                if (i >= speciesCount)
                    res = Brushes.Black;

                horizontalDots[i].Fill = res;
                verticalDots[i].Fill = res;
            }
        }
    
        public void UpdateCells(Vector4[] forces, int speciesCount, float maxForce)
        {
            UpdateDots();
            this.speciesCount = speciesCount;
            var inactive = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 32, 32, 32));
            for (int x = 0; x < Simulation.MaxSpeciesCount; x++)
            {
                for (int y = 0; y < Simulation.MaxSpeciesCount; y++)
                {
                    if (x < speciesCount && y < speciesCount)
                    {
                        var offset = Simulation.GetForceOffset(x, y);
                        double val = 0;
                        for (int i = 0; i < Simulation.KeypointsCount; i++)
                        {
                            val += forces[offset + i].Y;
                        }

                        var r = MathUtil.Amplify((val > 0) ? val / maxForce : 0, 4);
                        var b = MathUtil.Amplify((val < 0) ? -val / maxForce : 0, 4);
                        var g = Math.Max(r, b) / 6;
                        var rect = rectangles[x, y];
                        rect.Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, ClampColor(r), ClampColor(g), ClampColor(b)));
                    }
                    else
                    {
                        rectangles[x, y].Fill = inactive;
                    }
                 }
            }
        }

        private byte ClampColor(double x)
        {
            var c = (int)Math.Round(255 * x);
            if (c < 0)
                c = 0;
            if (c > 255)
                c = 255;
            return (byte)c;
        }
    }
}
