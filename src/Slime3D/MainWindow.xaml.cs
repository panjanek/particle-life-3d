using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Slime3D.Utils;
using Slime3D.Gpu;
using Slime3D.Gui;
using Slime3D.Models;
using AppContext = Slime3D.Models.AppContext;
using Application = System.Windows.Application;

namespace Slime3D
{
    using Models_AppContext = Models.AppContext;

    public partial class MainWindow : Window
    {
        private bool uiPending;

        private DateTime lastCheckTime;

        private long lastCheckFrameCount;

        private Models_AppContext app;

        private FullscreenWindow fullscreen;

        public MainWindow()
        {
            InitializeComponent();
            Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
        }

        private void parent_Loaded(object sender, RoutedEventArgs e)
        {
            app = new Models_AppContext();
            app.mainWindow = this;
            app.simulation = new Simulation();
            app.simulation.StartSimulation(5000, 2, 300);
            app.renderer = new OpenGlRenderer(placeholder, app);
            app.configWindow = new ConfigWindow(app);
            app.configWindow.Show();
            app.configWindow.Activate();

            KeyDown += MainWindow_KeyDown;
            System.Timers.Timer systemTimer = new System.Timers.Timer() { Interval = 10 };
            systemTimer.Elapsed += SystemTimer_Elapsed;
            systemTimer.Start();
            DispatcherTimer infoTimer = new DispatcherTimer() { Interval = TimeSpan.FromSeconds(1.0) };
            infoTimer.Tick += InfoTimer_Tick;
            infoTimer.Start();

        }
        public void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Space:
                    app.renderer.Paused = !app.renderer.Paused;
                    e.Handled = true;
                    break;
                case Key.Escape:
                    app.renderer.StopTracking();
                    e.Handled = true;
                    break;
                case Key.F:
                    ToggleFullscreen();
                    break;
            }
        }

        private void ToggleFullscreen()
        {
            if (fullscreen == null)
            {
                parent.Children.Remove(placeholder);
                fullscreen = new FullscreenWindow() { Owner = Window.GetWindow(this) };
                fullscreen.KeyDown += MainWindow_KeyDown;
                fullscreen.ContentHost.Content = placeholder;
                fullscreen.Show();
            }
            else
            {
                fullscreen.ContentHost.Content = null;
                parent.Children.Add(placeholder);
                fullscreen.Close();
                fullscreen = null;
            }
        }

        private void SystemTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (!uiPending)
            {
                uiPending = true;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        app.renderer.Step();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                    finally
                    {
                        uiPending = false;
                    }

                    uiPending = false;
                }), DispatcherPriority.Render);
            }
        }

        private void InfoTimer_Tick(object? sender, EventArgs e)
        {
            var now = DateTime.Now;
            var timespan = now - lastCheckTime;
            double frames = app.renderer.FrameCounter - lastCheckFrameCount;
            if (timespan.TotalSeconds >= 0.0001)
            {
                double fps = frames / timespan.TotalSeconds;
                Title = $"Slime3D. " +
                        $"fps:{fps.ToString("0.0")} "+
                        $"seed:{app.simulation.seed.ToString()} ";

                if (!string.IsNullOrWhiteSpace(app.configWindow.recordDir))
                {
                    Title += $"[recording to {app.configWindow.recordDir}] ";
                }

                lastCheckFrameCount = app.renderer.FrameCounter;
                lastCheckTime = now;
            }
        }
    }
}