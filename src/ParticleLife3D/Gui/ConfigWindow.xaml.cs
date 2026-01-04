using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.WindowsAPICodePack.Dialogs;
using ParticleLife3D.Models;
using ParticleLife3D.Utils;
using AppContext = ParticleLife3D.Models.AppContext;

namespace ParticleLife3D.Gui
{
    /// <summary>
    /// Interaction logic for ConfigWindow.xaml
    /// </summary>
    public unsafe partial class ConfigWindow : Window
    {
        private AppContext app;

        private bool updating;

        public string recordDir;
        public ConfigWindow(AppContext app)
        {
            this.app = app;
            InitializeComponent();
            customTitleBar.MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
            minimizeButton.Click += (s, e) => WindowState = WindowState.Minimized;
            Closing += (s, e) => { e.Cancel = true; WindowState = WindowState.Minimized; };
            ContentRendered += (s, e) => { UpdateActiveControls(); UpdatePassiveControls(); };
            forceMatrix.SelectionChanged = () =>
            {
                UpdateGraph();
                for (int i = 0; i < Simulation.MaxSpeciesCount; i++)
                    app.simulation.config.disabled[i] = forceMatrix.Disabled[i];
            };
            randomButton.PreviewKeyDown += (s, e) => e.Handled=true;
            restartButton.PreviewKeyDown += (s, e) => e.Handled = true;
            saveButton.PreviewKeyDown += (s, e) => e.Handled = true;
            loadButton.PreviewKeyDown += (s, e) => e.Handled = true;
            recordButton.PreviewKeyDown += (s, e) => e.Handled = true;
            backButton.PreviewKeyDown += (s, e) => e.Handled = true;
            backButton.Click += (s, e) => app.renderer.ResetOrigin();
            restartButton.Click += (s, e) => 
            { 
                app.simulation.InitializeParticles(app.simulation.config.particleCount);
                app.renderer.UploadParticleData();
                ResetMatrix();
            };

            randomButton.Click += (s, e) =>
            {
                app.simulation.InitializeParticles(app.simulation.config.particleCount);
                app.simulation.seed++;
                app.simulation.InitializeRandomForces();
                app.renderer.UploadParticleData();
                app.renderer.ResetOrigin();
                ResetMatrix();
            };

            saveButton.Click += (s, e) =>
            {
                var dialog = new CommonSaveFileDialog { Title = "Save configuration json file", DefaultExtension = "json" };
                dialog.Filters.Add(new CommonFileDialogFilter("JSON files", "*.json"));
                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    SimFactory.SaveToFile(app.simulation, dialog.FileName);
                    PopupMessage.Show(app.mainWindow, $"Config saved to {dialog.FileName}");
                }
            };

            loadButton.Click += (s, e) =>
            {
                var dialog = new CommonOpenFileDialog { Title = "Open configuration json file", DefaultExtension = "json" };
                dialog.Filters.Add(new CommonFileDialogFilter("JSON files", "*.json"));
                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    var newSim = SimFactory.LoadFromFile(dialog.FileName);
                    app.simulation = newSim;
                    app.renderer.UploadParticleData();
                    ResetMatrix();
                    UpdateActiveControls();
                    UpdatePassiveControls();
                    PopupMessage.Show(app.mainWindow, $"Config loaded from {dialog.FileName}");
                    if (app.renderer.Paused)
                    {
                        app.renderer.Paused = false;
                        app.renderer.Step();
                        app.renderer.Paused = true;
                    }
                }
            };

            forceGraph.Changed = () =>
            {
                var offset = Simulation.GetForceOffset(forceMatrix.SelectedX, forceMatrix.SelectedY);
                for (int i = 0; i < Simulation.KeypointsCount; i++)
                    app.simulation.forces[offset + i] = forceGraph.Forces[i];
                if (symetricCheckbox.IsChecked == true && forceMatrix.SelectedX != forceMatrix.SelectedY)
                {
                    var offset2 = Simulation.GetForceOffset(forceMatrix.SelectedY, forceMatrix.SelectedX);
                    for (int i = 0; i < Simulation.KeypointsCount; i++)
                        app.simulation.forces[offset2 + i] = forceGraph.Forces[i];
                }

                forceMatrix.UpdateCells(app.simulation.forces, app.simulation.config.speciesCount, app.simulation.config.maxForce);
            };

            invertButton.Click += (s, e) => Invert(Simulation.GetForceOffset(forceMatrix.SelectedX, forceMatrix.SelectedY));
            symetricButton.Click += (s, e) => CopyTo(Simulation.GetForceOffset(forceMatrix.SelectedX, forceMatrix.SelectedY), Simulation.GetForceOffset(forceMatrix.SelectedY, forceMatrix.SelectedX));
            asymetricButton.Click += (s, e) => 
            {
                if (forceMatrix.SelectedX != forceMatrix.SelectedY)
                {
                    CopyTo(Simulation.GetForceOffset(forceMatrix.SelectedX, forceMatrix.SelectedY), Simulation.GetForceOffset(forceMatrix.SelectedY, forceMatrix.SelectedX));
                    Invert(Simulation.GetForceOffset(forceMatrix.SelectedY, forceMatrix.SelectedX));
                }
            };

            everythingSymetricButton.Click += (s, e) =>
            {
                for (int i = 0; i < app.simulation.config.speciesCount; i++)
                    for(int j=0; j<i; j++)
                        CopyTo(Simulation.GetForceOffset(i,j), Simulation.GetForceOffset(j,i));

            };

            KeyDown += (s, e) => app.mainWindow.MainWindow_KeyDown(s, e);
        }

        private void Invert(int offset)
        {
            for (int i = 1; i < Simulation.KeypointsCount; i++)
                app.simulation.forces[offset + i].Y *= -1;
            UpdateActiveControls();
            UpdatePassiveControls();
        }

        private void CopyTo(int fromOffset, int toOffset)
        {
            if (fromOffset != toOffset)
            {
                for (int i = 0; i < Simulation.KeypointsCount; i++)
                    app.simulation.forces[toOffset+i] = app.simulation.forces[fromOffset+i];
                UpdateActiveControls();
                UpdatePassiveControls();
            }
        }

        private void Record_Click(object sender, RoutedEventArgs e)
        {
            if (recordButton.IsChecked == true)
            {
                var dialog = new CommonOpenFileDialog { IsFolderPicker = true, Title = "Select folder to save frames as PNG files" };
                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                    recordDir = dialog.FileName;
                else
                    recordButton.IsChecked = false;
            }
            else
            {
                recordDir = null;
            }

            e.Handled = true;
        }

        private void ResetMatrix()
        {
            if (forceMatrix.SelectedX >= app.simulation.config.speciesCount || forceMatrix.SelectedY >= app.simulation.config.speciesCount)
            {
                forceMatrix.SelectedX = 0;
                forceMatrix.SelectedY = 0;
            }

            forceMatrix.UpdateCells(app.simulation.forces, app.simulation.config.speciesCount, app.simulation.config.maxForce);
            forceMatrix.UpdateSelection();
            UpdateGraph();
        }

        private void UpdateGraph()
        {
            var offset = Simulation.GetForceOffset(forceMatrix.SelectedX, forceMatrix.SelectedY);
            var forces = app.simulation.forces.Skip(offset).Take(Simulation.KeypointsCount).ToArray();
            forceGraph.UpdateGraph(forces, app.simulation.config.maxDist, app.simulation.config.maxForce);
        }

        private void global_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (fieldSize != null && particlesCount != null && speciesCount!=null && !updating)
            {
                var newParticleCountStr = WpfUtil.GetComboSelectionAsString(particlesCount);
                var newSpeciesCountStr = WpfUtil.GetComboSelectionAsString(speciesCount);
                var newSizeStr = WpfUtil.GetComboSelectionAsString(fieldSize);
                if (!string.IsNullOrWhiteSpace(newParticleCountStr) && !string.IsNullOrWhiteSpace(newSpeciesCountStr) && !string.IsNullOrWhiteSpace(newSizeStr))
                {
                    var newParticleCount = int.Parse(newParticleCountStr);
                    var newSpeciesCount = int.Parse(newSpeciesCountStr);
                    var sizeSplit = newSizeStr.Split('x');
                    var newSize = int.Parse(sizeSplit[0]);
                    if (newParticleCount != app.simulation.config.particleCount ||
                        newSpeciesCount != app.simulation.config.speciesCount ||
                        newSize != app.simulation.config.fieldSize)
                    {
                        app.simulation.StartSimulation(newParticleCount, newSpeciesCount, newSize);
                        app.renderer.UploadParticleData();
                        ResetMatrix();
                        UpdateActiveControls();
                        UpdatePassiveControls();
                        if (app.renderer.Paused)
                        {
                            app.renderer.Paused = false;
                            app.renderer.Step();
                            app.renderer.Paused = true;
                        }
                    }
                }

            }
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!updating)
            {
                var tag = WpfUtil.GetTagAsString(sender);
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    ReflectionUtil.SetObjectValue<float>(app.simulation, tag, (float)e.NewValue);
                    UpdatePassiveControls();
                }
            }
        }

        private void infoText_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var tag = WpfUtil.GetTagAsString(sender);
            if (!string.IsNullOrWhiteSpace(tag))
                WpfUtil.FindVisualChildren<Slider>(this).Where(s => WpfUtil.GetTagAsString(s) == tag).FirstOrDefault()?.Focus();
            e.Handled = true;
        }

        public void UpdateActiveControls()
        {
            updating = true;
            WpfUtil.SetComboStringSelection(fieldSize, $"{app.simulation.config.fieldSize}x{app.simulation.config.fieldSize}x{app.simulation.config.fieldSize}");
            WpfUtil.SetComboStringSelection(particlesCount, app.simulation.config.particleCount.ToString());
            WpfUtil.SetComboStringSelection(speciesCount, app.simulation.config.speciesCount.ToString());
            foreach (var slider in WpfUtil.FindVisualChildren<Slider>(this))
            {
                var tag = WpfUtil.GetTagAsString(slider);
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    slider.Value = ReflectionUtil.GetObjectValue<float>(app.simulation, tag);
                }
            }
            UpdateGraph();
            updating = false;
        }

        public void UpdatePassiveControls()
        {
            foreach (var text in WpfUtil.FindVisualChildren<TextBlock>(this))
                    WpfUtil.UpdateTextBlockForSlider(this, text, app.simulation);
            forceMatrix.UpdateCells(app.simulation.forces, app.simulation.config.speciesCount, app.simulation.config.maxForce);
            UpdateGraph();
            forceMatrix.UpdateDots();
        }
    }
}
