using System.IO;
using System.Windows;
using System.Windows.Controls;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using WpfButton = System.Windows.Controls.Button;
using WpfMessageBox = System.Windows.MessageBox;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfTextBox = System.Windows.Controls.TextBox;
using LmuOverlay.Widgets;
using LmuOverlay.Core;

namespace LmuOverlay.Desktop;

public partial class ConfigurationWindow
{
    private void CustomColorChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not WpfTextBox input)
        {
            return;
        }

        var normalized = OverlayVisualSystem.NormalizeHexColor(input.Text, string.Empty);
        var valid = normalized.Length == 7;
        input.BorderBrush = valid
            ? new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(normalized)!)
            : System.Windows.Media.Brushes.IndianRed;
        input.BorderThickness = new Thickness(valid ? 2 : 1);

        if (_loadingProfile || !valid)
        {
            return;
        }

        foreach (var item in ThemeSelector.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), "Custom", StringComparison.Ordinal))
            {
                ThemeSelector.SelectedItem = item;
                break;
            }
        }
    }

    private void LanguageSelector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ApplyLocalization();
    }

    private void ApplyLocalization()
    {
        var language = (LanguageSelector.SelectedItem as ComboBoxItem)?.Tag?.ToString()
            ?? _overlay.CurrentProfile.Settings.Language;
        OverlayLocalization.Apply(this, language);
        Title = OverlayText.Get(language, OverlayTextKey.ConfigureWidgets);
    }

    private static double ParseDouble(
        string text,
        double fallback,
        double minimum,
        double maximum) =>
        double.TryParse(
            text,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value)
            ? Math.Clamp(value, minimum, maximum)
            : fallback;

    private static int ParseInt(
        string text,
        int fallback,
        int minimum,
        int maximum) =>
        int.TryParse(
            text,
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value)
            ? Math.Clamp(value, minimum, maximum)
            : fallback;

    private static string SelectedTag(System.Windows.Controls.ComboBox comboBox, string fallback) =>
        (comboBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag?.ToString() ?? fallback;

    private static void SelectTaggedItem(System.Windows.Controls.ComboBox comboBox, string tag)
    {
        comboBox.SelectedItem = comboBox.Items.OfType<System.Windows.Controls.ComboBoxItem>().FirstOrDefault(item =>
            string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase));
    }
}
