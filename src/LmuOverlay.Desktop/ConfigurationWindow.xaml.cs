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

namespace LmuOverlay.Desktop;

public partial class ConfigurationWindow : Window
{
    private readonly OverlayWindow _overlay;
    private bool _refreshingProfiles;
    private bool _loadingProfile;

    public ConfigurationWindow(OverlayWindow overlay)
    {
        _overlay = overlay;
        InitializeComponent();
        PresetSelector.ItemsSource = LayoutPresets.Names;
        PresetSelector.SelectedIndex = 0;
        RefreshProfiles();
        LoadProfile();
        ApplyLocalization();
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
        _loadingProfile = true;
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
        foreach (var item in LanguageSelector.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), profile.Settings.Language, StringComparison.OrdinalIgnoreCase))
            {
                LanguageSelector.SelectedItem = item;
                break;
            }
        }
        RefreshRate.Value = profile.Settings.RefreshRateHz;
        GridSnap.Value = profile.Settings.GridSnapPixels;
        BackgroundOpacity.Value = profile.Settings.BackgroundOpacity;
        DashboardTitle.Text = profile.Settings.DashboardTitle;
        CustomAccentColor.Text = profile.Settings.CustomAccentColor;
        CustomBackgroundColor.Text = profile.Settings.CustomBackgroundColor;
        CustomCardColor.Text = profile.Settings.CustomCardColor;
        CustomPrimaryTextColor.Text = profile.Settings.CustomPrimaryTextColor;
        CustomSecondaryTextColor.Text = profile.Settings.CustomSecondaryTextColor;
        CustomInformationColor.Text = profile.Settings.CustomInformationColor;
        CustomAttentionColor.Text = profile.Settings.CustomAttentionColor;
        CustomCriticalColor.Text = profile.Settings.CustomCriticalColor;
        CustomPositiveColor.Text = profile.Settings.CustomPositiveColor;
        DashboardShowSectors.IsChecked = profile.Settings.DashboardShowSectors;
        DashboardShowTires.IsChecked = profile.Settings.DashboardShowTires;
        DashboardShowTelemetry.IsChecked = profile.Settings.DashboardShowTelemetry;
        var moduleOrder = DashboardModuleLayout.Parse(profile.Settings.DashboardModuleOrder);
        SelectTaggedItem(DashboardModule1, moduleOrder[0].ToString());
        SelectTaggedItem(DashboardModule2, moduleOrder[1].ToString());
        SelectTaggedItem(DashboardModule3, moduleOrder[2].ToString());
        DashboardTextScale.Value = profile.Settings.DashboardTextScale;
        TimingTextScale.Value = profile.Settings.TimingTextScale;
        InputsTextScale.Value = profile.Settings.InputsTextScale;
        LiveStandingsMaximumRows.Value = profile.Settings.LiveStandingsMaximumRows;
        RelativeCarsEachSide.Value = profile.Settings.RelativeCarsEachSide;
        PedalHistory.Value = profile.Settings.PedalHistorySeconds;
        PriorityAlerts.IsChecked = profile.Settings.ShowPriorityAlerts;
        ReduceMotion.IsChecked = profile.Settings.ReduceMotion;
        EnableOfficialTimingHttp.IsChecked = profile.Settings.EnableOfficialTimingHttp;
        EnableNativeRendering.IsChecked = profile.Settings.EnableNativeRendering;
        EnableSteamVr.IsChecked = profile.Settings.EnableSteamVr;
        foreach (var item in DensitySelector.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(
                item.Tag?.ToString(),
                profile.Settings.VisualDensity,
                StringComparison.OrdinalIgnoreCase))
            {
                DensitySelector.SelectedItem = item;
                break;
            }
        }
        FuelReserve.Value = profile.Settings.FuelReserveLaps;
        EnergyReserve.Text = profile.Settings.EnergyReservePercent.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        ManualRemainingLaps.Text = profile.Settings.ManualRemainingLaps.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        ManualRemainingMinutes.Text = profile.Settings.ManualRemainingMinutes.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        ManualLapTimeSeconds.Text = profile.Settings.ManualLapTimeSeconds.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        ManualFuelPerLapLiters.Text = profile.Settings.ManualFuelPerLapLiters.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        ManualFuelCapacityLiters.Text = profile.Settings.ManualFuelCapacityLiters.ToString(
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
        _loadingProfile = false;
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

    private void ApplyPresetClicked(object sender, RoutedEventArgs e)
    {
        if (PresetSelector.SelectedItem is not string name)
        {
            return;
        }

        RunProfileAction(() =>
        {
            _overlay.ApplyPreset(name);
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
        var customColorsChanged = new[]
        {
            (CustomAccentColor.Text, current.CustomAccentColor),
            (CustomBackgroundColor.Text, current.CustomBackgroundColor),
            (CustomCardColor.Text, current.CustomCardColor),
            (CustomPrimaryTextColor.Text, current.CustomPrimaryTextColor),
            (CustomSecondaryTextColor.Text, current.CustomSecondaryTextColor),
            (CustomInformationColor.Text, current.CustomInformationColor),
            (CustomAttentionColor.Text, current.CustomAttentionColor),
            (CustomCriticalColor.Text, current.CustomCriticalColor),
            (CustomPositiveColor.Text, current.CustomPositiveColor),
        }.Any(pair => !string.Equals(
            OverlayVisualSystem.NormalizeHexColor(pair.Item1, pair.Item2),
            pair.Item2,
            StringComparison.OrdinalIgnoreCase));
        if (customColorsChanged)
        {
            theme = "Custom";
        }
        var density = (DensitySelector.SelectedItem as ComboBoxItem)?.Tag?.ToString()
            ?? "Auto";
        return current with
        {
            Language = OverlayText.Normalize(
                (LanguageSelector.SelectedItem as ComboBoxItem)?.Tag?.ToString()),
            Theme = theme,
            DashboardTitle = DashboardTitle.Text,
            CustomAccentColor = CustomAccentColor.Text,
            CustomBackgroundColor = CustomBackgroundColor.Text,
            CustomCardColor = CustomCardColor.Text,
            CustomPrimaryTextColor = CustomPrimaryTextColor.Text,
            CustomSecondaryTextColor = CustomSecondaryTextColor.Text,
            CustomInformationColor = CustomInformationColor.Text,
            CustomAttentionColor = CustomAttentionColor.Text,
            CustomCriticalColor = CustomCriticalColor.Text,
            CustomPositiveColor = CustomPositiveColor.Text,
            DashboardShowSectors = DashboardShowSectors.IsChecked == true,
            DashboardShowTires = DashboardShowTires.IsChecked == true,
            DashboardShowTelemetry = DashboardShowTelemetry.IsChecked == true,
            DashboardModuleOrder = DashboardModuleLayout.Normalize(string.Join(',', new[]
            {
                SelectedTag(DashboardModule1, "Sectors"),
                SelectedTag(DashboardModule2, "Tires"),
                SelectedTag(DashboardModule3, "Telemetry"),
            })),
            DashboardTextScale = DashboardTextScale.Value,
            TimingTextScale = TimingTextScale.Value,
            InputsTextScale = InputsTextScale.Value,
            LiveStandingsMaximumRows = (int)Math.Round(LiveStandingsMaximumRows.Value),
            RelativeCarsEachSide = (int)Math.Round(RelativeCarsEachSide.Value),
            VisualDensity = density,
            RefreshRateHz = (int)Math.Round(RefreshRate.Value),
            GridSnapPixels = (int)Math.Round(GridSnap.Value),
            BackgroundOpacity = BackgroundOpacity.Value,
            PedalHistorySeconds = (int)Math.Round(PedalHistory.Value),
            ShowPriorityAlerts = PriorityAlerts.IsChecked == true,
            ReduceMotion = ReduceMotion.IsChecked == true,
            EnableOfficialTimingHttp = EnableOfficialTimingHttp.IsChecked == true,
            EnableNativeRendering = EnableNativeRendering.IsChecked == true,
            EnableSteamVr = EnableSteamVr.IsChecked == true,
            FuelReserveLaps = FuelReserve.Value,
            EnergyReservePercent = ParseDouble(EnergyReserve.Text, 2, 0, 25),
            ManualRemainingLaps = ParseInt(ManualRemainingLaps.Text, 0, 0, 1000),
            ManualRemainingMinutes = ParseDouble(ManualRemainingMinutes.Text, 0, 0, 1440),
            ManualLapTimeSeconds = ParseDouble(ManualLapTimeSeconds.Text, 0, 0, 3600),
            ManualFuelPerLapLiters = ParseDouble(ManualFuelPerLapLiters.Text, 0, 0, 100),
            ManualFuelCapacityLiters = ParseDouble(ManualFuelCapacityLiters.Text, 0, 0, 1000),
            MaximumStintLaps = ParseInt(MaximumStintLaps.Text, 0, 0, 1000),
            EstimatedPitLossSeconds = ParseDouble(PitLossSeconds.Text, 30, 0, 600),
            AvailableTireSets = ParseInt(AvailableTireSets.Text, 0, 0, 100),
            TireWearLimitPercent = ParseDouble(TireWearLimit.Text, 70, 20, 95),
            EstimatedTireChangeSeconds = ParseDouble(TireChangeSeconds.Text, 15, 0, 180),
        };
    }

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
