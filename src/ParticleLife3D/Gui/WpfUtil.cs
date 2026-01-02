using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ParticleLife3D.Utils;
using Brushes = System.Windows.Media.Brushes;
using ComboBox = System.Windows.Controls.ComboBox;

namespace ParticleLife3D.Gui
{
    public static class WpfUtil
    {
        public static string GetComboSelectionAsString(ComboBox combo)
        {
            if (combo.SelectedItem is ComboBoxItem)
            {
                var item = (ComboBoxItem)combo.SelectedItem;
                return item.Content?.ToString();
            }

            return null;
        }

        public static int GetComboSelectionAsInt(ComboBox combo)
        {
            var str = GetComboSelectionAsString(combo);
            return int.Parse(str);
        }

        public static void SetComboStringSelection(ComboBox combo, string value)
        {
            foreach (var item in combo.Items)
            {
                if (item is ComboBoxItem)
                {
                    var comboItem = item as ComboBoxItem;
                    comboItem.IsSelected = comboItem.Content?.ToString() == value;
                }
            }
        }

        public static string GetTagAsString(object element)
        {
            if (element is FrameworkElement)
            {
                var el = (FrameworkElement)element;
                if (el.Tag is string)
                    return el.Tag as string;
                else
                    return null;
            }
            else
                return null;
        }

        public static int GetTagAsInt(object element)
        {
            return int.Parse(GetTagAsString(element));
        }

        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
        where T : DependencyObject
        {
            if (parent == null)
                yield break;

            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                if (child is T t)
                    yield return t;

                foreach (var descendant in FindVisualChildren<T>(child))
                    yield return descendant;
            }
        }

        public static void UpdateTextBlockForSlider(FrameworkElement parent, TextBlock text, object recipe)
        {
            var tag = WpfUtil.GetTagAsString(text);
            if (!string.IsNullOrWhiteSpace(tag))
            {
                string format = "0.000";
                var slider = WpfUtil.FindVisualChildren<Slider>(parent).FirstOrDefault(s => WpfUtil.GetTagAsString(s) == tag);
                if (slider != null)
                {
                    switch (slider.SmallChange)
                    {
                        case 1:
                            format = "0";
                            break;
                        case 0.1:
                            format = "0.0";
                            break;
                        case 0.01:
                            format = "0.00";
                            break;
                        case 0.001:
                            format = "0.000";
                            break;
                        case 0.0001:
                            format = "0.0000";
                            break;
                    }
                }

                var value = ReflectionUtil.GetObjectValue<float>(recipe, tag);
                text.Text = value.ToString(format, CultureInfo.InvariantCulture);
                text.Background = Brushes.Black;
                text.Foreground = Brushes.White;
            }
        }
    }
}
