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
            SteeringWheelImagePath = SteeringWheelImagePath.Text,
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
}
