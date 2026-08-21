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
        Set(
            PriorityAlerts,
            PriorityAlertOpacity,
            PriorityAlertScale,
            profile.PriorityAlert);
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
        SteeringWheelImagePath.Text = profile.Settings.SteeringWheelImagePath;
        SteeringWheelRangeDegrees.Text =
            profile.Settings.SteeringWheelRangeDegrees.ToString("0");
        UseDirectSteeringInput.IsChecked = profile.Settings.UseDirectSteeringInput;
        SteeringInputDeviceId.Text = profile.Settings.SteeringInputDeviceId.ToString();
        var steeringDevices = WindowsSteeringInputReader.EnumerateDevices();
        SteeringDevicesText.Text = steeringDevices.Count == 0
            ? "°  (0 = automático; nenhum dispositivo Windows encontrado)"
            : "°  Dispositivos: " + string.Join(" · ",
                steeringDevices.Select(device => $"{device.Id}: {device.Name}"));
        LiveStandingsMaximumRows.Value = profile.Settings.LiveStandingsMaximumRows;
        RelativeCarsEachSide.Value = profile.Settings.RelativeCarsEachSide;
        PedalHistory.Value = profile.Settings.PedalHistorySeconds;
        PriorityAlerts.IsChecked = profile.Settings.ShowPriorityAlerts &&
            profile.PriorityAlert.Visible;
        ReduceMotion.IsChecked = profile.Settings.ReduceMotion;
        EnableOfficialTimingHttp.IsChecked = profile.Settings.EnableOfficialTimingHttp;
        EnableNativeRendering.IsChecked = profile.Settings.EnableNativeRendering;
        EnableSteamVr.IsChecked = profile.Settings.EnableSteamVr;
        AllowMultiMonitorPlacement.IsChecked = profile.Settings.AllowMultiMonitorPlacement;
        EnableRemoteDashboard.IsChecked = profile.Settings.EnableRemoteDashboard;
        RemoteDashboardPort.Text = profile.Settings.RemoteDashboardPort.ToString();
        RemoteDashboardAddress.Text = RemoteDashboardServer.DisplayUrl(
            profile.Settings.RemoteDashboardPort,
            profile.Settings.RemoteDashboardToken);
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
}
