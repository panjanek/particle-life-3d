using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace ParticleLife3D.Gui
{
    public static class PopupMessage
    {
        private static Window current;
        public static void Show(
            Window owner,
            string message,
            int displayMilliseconds = 1500)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));

            current?.Close();

            var popup = new Window
            {
                Owner = owner,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ShowInTaskbar = false,
                SizeToContent = SizeToContent.WidthAndHeight,
                Topmost = true,
                Opacity = 0,
                ShowActivated = false
            };

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20),
                Child = new TextBlock
                {
                    Text = message,
                    Foreground = Brushes.White,
                    FontSize = 16,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center
                }
            };

            popup.Content = border;

            popup.Loaded += (_, __) =>
            {
                CenterOverOwner(popup, owner);
                BeginAnimation(popup, displayMilliseconds);
            };

            current = popup;
            popup.Show();
        }

        private static void CenterOverOwner(Window popup, Window owner)
        {
            popup.Left = owner.Left + (owner.ActualWidth - popup.ActualWidth) / 2;
            popup.Top = owner.Top + (owner.ActualHeight - popup.ActualHeight) / 2;
        }

        private static void BeginAnimation(Window popup, int displayMilliseconds)
        {
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(100)
            };

            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                BeginTime = TimeSpan.FromMilliseconds(displayMilliseconds),
                Duration = TimeSpan.FromMilliseconds(500)
            };

            fadeOut.Completed += (_, __) => { popup.Close(); current = null; };

            var storyboard = new Storyboard();
            storyboard.Children.Add(fadeIn);
            storyboard.Children.Add(fadeOut);

            Storyboard.SetTarget(fadeIn, popup);
            Storyboard.SetTarget(fadeOut, popup);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath(Window.OpacityProperty));
            Storyboard.SetTargetProperty(fadeOut, new PropertyPath(Window.OpacityProperty));

            storyboard.Begin();
        }
    }
}
