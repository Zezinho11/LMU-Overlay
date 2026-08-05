using System.IO;
using System.Windows;
using System.Windows.Controls;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using WpfButton = System.Windows.Controls.Button;
using WpfMessageBox = System.Windows.MessageBox;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace LmuOverlay.Desktop;

public partial class ConfigurationWindow : Window
{
    private readonly OverlayWindow _overlay;
    private bool _refreshingProfiles;

    public ConfigurationWindow(OverlayWindow overlay)
    {
        _overlay = overlay;
        InitializeComponent();
        RefreshProfiles();
        LoadProfile();
    }

    private void RefreshProfiles()
    {
        _refreshingProfiles = true;
        ProfileSelector.ItemsSource = _overlay.ProfileNames;
        ProfileSelector.SelectedItem = _overlay.ActiveProfileName;
        _refreshingProfiles = false;
    }

    private void LoadProfile()
    {
        var profile = _overlay.CurrentProfile;
        Set(DashboardVisible, DashboardOpacity, DashboardScale, profile.Diagnostic);
        Set(InputsVisible, InputsOpacity, InputsScale, profile.Inputs);
        Set(StandingsVisible, StandingsOpacity, StandingsScale, profile.LiveStandings);
        Set(RelativeVisible, RelativeOpacity, RelativeScale, profile.Relative);
        Set(SessionVisible, SessionOpacity, SessionScale, profile.SessionFlags);
        Set(FuelVisible, FuelOpacity, FuelScale, profile.FuelStrategy);
        Set(
            RaceControlVisible,
            RaceControlOpacity,
            RaceControlScale,
            profile.RaceControl);
        foreach (var item in ThemeSelector.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), profile.Settings.Theme, StringComparison.Ordinal))
            {
                ThemeSelector.SelectedItem = item;
                break;
            }
        }
        RefreshRate.Value = profile.Settings.RefreshRateHz;
        GridSnap.Value = profile.Settings.GridSnapPixels;
        FuelReserve.Value = profile.Settings.FuelReserveLaps;
        EnergyReserve.Text = profile.Settings.EnergyReservePercent.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        ManualRemainingLaps.Text = profile.Settings.ManualRemainingLaps.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        MaximumStintLaps.Text = profile.Settings.MaximumStintLaps.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        PitLossSeconds.Text = profile.Settings.EstimatedPitLossSeconds.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        AvailableTireSets.Text = profile.Settings.AvailableTireSets.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        TireWearLimit.Text = profile.Settings.TireWearLimitPercent.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        TireChangeSeconds.Text = profile.Settings.EstimatedTireChangeSeconds.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private void ProfileSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshingProfiles || ProfileSelector.SelectedItem is not string name)
        {
            return;
        }

        RunProfileAction(() =>
        {
            _overlay.SwitchProfile(name);
            LoadProfile();
        });
    }

    private void NewProfileClicked(object sender, RoutedEventArgs e)
    {
        var name = PromptProfileName("Novo perfil", "Novo layout");
        if (name is null)
        {
            return;
        }

        RunProfileAction(() =>
        {
            _overlay.CreateProfile(name, false);
            RefreshProfiles();
            LoadProfile();
        });
    }

    private void DuplicateProfileClicked(object sender, RoutedEventArgs e)
    {
        var name = PromptProfileName(
            "Duplicar perfil",
            $"{_overlay.ActiveProfileName} - cópia");
        if (name is null)
        {
            return;
        }

        RunProfileAction(() =>
        {
            _overlay.CreateProfile(name, true);
            RefreshProfiles();
            LoadProfile();
        });
    }

    private void RenameProfileClicked(object sender, RoutedEventArgs e)
    {
        var name = PromptProfileName("Renomear perfil", _overlay.ActiveProfileName);
        if (name is null)
        {
            return;
        }

        RunProfileAction(() =>
        {
            _overlay.RenameProfile(name);
            RefreshProfiles();
        });
    }

    private void DeleteProfileClicked(object sender, RoutedEventArgs e)
    {
        var result = WpfMessageBox.Show(
            this,
            $"Excluir o perfil '{_overlay.ActiveProfileName}'?",
            "Excluir perfil",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        RunProfileAction(() =>
        {
            _overlay.DeleteActiveProfile();
            RefreshProfiles();
            LoadProfile();
        });
    }

    private void ImportProfileClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Importar perfil do LMU Overlay",
            Filter = "Perfil do LMU Overlay (*.lmu-layout.json)|*.lmu-layout.json|JSON (*.json)|*.json",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        RunProfileAction(() =>
        {
            _overlay.ImportProfile(dialog.FileName);
            RefreshProfiles();
            LoadProfile();
        });
    }

    private void ExportProfileClicked(object sender, RoutedEventArgs e)
    {
        var safeName = string.Concat(_overlay.ActiveProfileName.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '-' : character));
        var dialog = new SaveFileDialog
        {
            Title = "Exportar perfil do LMU Overlay",
            Filter = "Perfil do LMU Overlay (*.lmu-layout.json)|*.lmu-layout.json",
            FileName = $"{safeName}.lmu-layout.json",
            AddExtension = true,
            DefaultExt = ".lmu-layout.json",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        RunProfileAction(
            () => _overlay.ExportActiveProfile(dialog.FileName),
            "Perfil exportado com sucesso.");
    }

    private void ExportDiagnosticsClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Exportar diagnóstico do LMU Overlay",
            Filter = "Diagnóstico JSON (*.json)|*.json",
            FileName = $"lmu-overlay-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.json",
            AddExtension = true,
            DefaultExt = ".json",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        RunProfileAction(
            () => _overlay.ExportDiagnostics(dialog.FileName),
            "Diagnóstico exportado sem nomes de pilotos ou telemetria bruta.");
    }

    private void ApplyClicked(object sender, RoutedEventArgs e)
    {
        var profile = _overlay.CurrentProfile;
        _overlay.ApplyDisplaySettings(profile with
        {
            Diagnostic = Read(
                DashboardVisible,
                DashboardOpacity,
                DashboardScale,
                profile.Diagnostic),
            Inputs = Read(InputsVisible, InputsOpacity, InputsScale, profile.Inputs),
            LiveStandings = Read(
                StandingsVisible,
                StandingsOpacity,
                StandingsScale,
                profile.LiveStandings),
            Relative = Read(
                RelativeVisible,
                RelativeOpacity,
                RelativeScale,
                profile.Relative),
            SessionFlags = Read(
                SessionVisible,
                SessionOpacity,
                SessionScale,
                profile.SessionFlags),
            FuelStrategy = Read(
                FuelVisible,
                FuelOpacity,
                FuelScale,
                profile.FuelStrategy),
            RaceControl = Read(
                RaceControlVisible,
                RaceControlOpacity,
                RaceControlScale,
                profile.RaceControl),
            Settings = ReadSettings(profile.Settings),
        });
    }

    private void ResetClicked(object sender, RoutedEventArgs e)
    {
        _overlay.ResetLayout();
        LoadProfile();
    }

    private void CloseClicked(object sender, RoutedEventArgs e) => Close();

    private void RunProfileAction(Action action, string? successMessage = null)
    {
        try
        {
            action();
            if (successMessage is not null)
            {
                WpfMessageBox.Show(
                    this,
                    successMessage,
                    "LMU Overlay",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            WpfMessageBox.Show(
                this,
                exception.Message,
                "Não foi possível concluir",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            RefreshProfiles();
        }
    }

    private string? PromptProfileName(string title, string initialValue)
    {
        var input = new WpfTextBox
        {
            Text = initialValue,
            Margin = new Thickness(0, 10, 0, 16),
            MinWidth = 330,
            Padding = new Thickness(7, 5, 7, 5),
        };
        var dialog = new Window
        {
            Owner = this,
            Title = title,
            Width = 410,
            Height = 175,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(11, 17, 23)),
            Foreground = System.Windows.Media.Brushes.White,
        };
        var confirm = new WpfButton
        {
            Content = "Confirmar",
            IsDefault = true,
            MinWidth = 90,
            Padding = new Thickness(12, 6, 12, 6),
        };
        var cancel = new WpfButton
        {
            Content = "Cancelar",
            IsCancel = true,
            MinWidth = 90,
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(12, 6, 12, 6),
        };
        confirm.Click += (_, _) => dialog.DialogResult = true;
        var buttons = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(confirm);
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = "Nome do perfil" });
        panel.Children.Add(input);
        panel.Children.Add(buttons);
        dialog.Content = panel;
        dialog.Loaded += (_, _) =>
        {
            input.Focus();
            input.SelectAll();
        };

        return dialog.ShowDialog() == true ? input.Text : null;
    }

    private static void Set(
        System.Windows.Controls.CheckBox visible,
        Slider opacity,
        Slider scale,
        WidgetPlacement placement)
    {
        visible.IsChecked = placement.Visible;
        opacity.Value = placement.Opacity;
        scale.Value = placement.Scale;
    }

    private static WidgetPlacement Read(
        System.Windows.Controls.CheckBox visible,
        Slider opacity,
        Slider scale,
        WidgetPlacement placement) => placement with
    {
        Visible = visible.IsChecked == true,
        Opacity = opacity.Value,
        Scale = scale.Value,
    };

    private OverlayProfileSettings ReadSettings(OverlayProfileSettings current)
    {
        var theme = (ThemeSelector.SelectedItem as ComboBoxItem)?.Tag?.ToString()
            ?? "RedFox";
        return current with
        {
            Theme = theme,
            RefreshRateHz = (int)Math.Round(RefreshRate.Value),
            GridSnapPixels = (int)Math.Round(GridSnap.Value),
            FuelReserveLaps = FuelReserve.Value,
            EnergyReservePercent = ParseDouble(EnergyReserve.Text, 2, 0, 25),
            ManualRemainingLaps = ParseInt(ManualRemainingLaps.Text, 0, 0, 1000),
            MaximumStintLaps = ParseInt(MaximumStintLaps.Text, 0, 0, 1000),
            EstimatedPitLossSeconds = ParseDouble(PitLossSeconds.Text, 30, 0, 600),
            AvailableTireSets = ParseInt(AvailableTireSets.Text, 0, 0, 100),
            TireWearLimitPercent = ParseDouble(TireWearLimit.Text, 70, 20, 95),
            EstimatedTireChangeSeconds = ParseDouble(TireChangeSeconds.Text, 15, 0, 180),
        };
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
}
