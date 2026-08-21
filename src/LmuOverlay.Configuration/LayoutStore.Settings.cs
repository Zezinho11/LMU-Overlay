using System.IO;
using System.Text.Json;

namespace LmuOverlay.Configuration;

public sealed partial class LayoutStore
{
    public static OverlayProfileSettings SanitizeSettings(
        OverlayProfileSettings? settings)
    {
        settings ??= new();
        var theme = settings.Theme is "RedFox" or "HighContrast" or "ColorVisionSafe" or "Black" or "Custom"
            ? settings.Theme
            : "RedFox";
        var density = Enum.TryParse<OverlayDensity>(
            settings.VisualDensity,
            true,
            out var parsedDensity)
                ? parsedDensity.ToString()
                : OverlayDensity.Auto.ToString();
        return settings with
        {
            Language = OverlayText.Normalize(settings.Language),
            Theme = theme,
            CustomAccentColor = ProfileValueSanitizer.NormalizeHexColor(
                settings.CustomAccentColor,
                "#42D3A6"),
            CustomBackgroundColor = ProfileValueSanitizer.NormalizeHexColor(
                settings.CustomBackgroundColor,
                "#0A0F1A"),
            CustomCardColor = ProfileValueSanitizer.NormalizeHexColor(
                settings.CustomCardColor,
                "#121924"),
            CustomPrimaryTextColor = ProfileValueSanitizer.NormalizeHexColor(
                settings.CustomPrimaryTextColor,
                "#FFFFFF"),
            CustomSecondaryTextColor = ProfileValueSanitizer.NormalizeHexColor(
                settings.CustomSecondaryTextColor,
                "#CAD3DC"),
            CustomInformationColor = ProfileValueSanitizer.NormalizeHexColor(
                settings.CustomInformationColor,
                "#12D9E5"),
            CustomAttentionColor = ProfileValueSanitizer.NormalizeHexColor(
                settings.CustomAttentionColor,
                "#FFBE40"),
            CustomCriticalColor = ProfileValueSanitizer.NormalizeHexColor(
                settings.CustomCriticalColor,
                "#FF464B"),
            CustomPositiveColor = ProfileValueSanitizer.NormalizeHexColor(
                settings.CustomPositiveColor,
                "#42D3A6"),
            DashboardTitle = SanitizeDashboardTitle(settings.DashboardTitle),
            DashboardModuleOrder = DashboardModuleLayout.Normalize(
                settings.DashboardModuleOrder),
            DashboardTextScale = Math.Clamp(
                settings.DashboardTextScale <= 0 ? 1 : settings.DashboardTextScale,
                0.8,
                1.25),
            TimingTextScale = Math.Clamp(
                settings.TimingTextScale <= 0 ? 1 : settings.TimingTextScale,
                0.8,
                1.25),
            InputsTextScale = Math.Clamp(
                settings.InputsTextScale <= 0 ? 1 : settings.InputsTextScale,
                0.8,
                1.25),
            SteeringWheelImagePath = SanitizeSteeringWheelImagePath(
                settings.SteeringWheelImagePath),
            SteeringWheelRangeDegrees =
                settings.SteeringWheelRangeDegrees is >= 180 and <= 1440
                    ? settings.SteeringWheelRangeDegrees
                    : 0,
            SteeringInputDeviceId = Math.Clamp(settings.SteeringInputDeviceId, -1, 15),
            LiveStandingsMaximumRows = Math.Clamp(
                settings.LiveStandingsMaximumRows <= 0 ? 12 : settings.LiveStandingsMaximumRows,
                6,
                12),
            RelativeCarsEachSide = Math.Clamp(
                settings.RelativeCarsEachSide <= 0 ? 4 : settings.RelativeCarsEachSide,
                2,
                5),
            VisualDensity = density,
            RefreshRateHz = Math.Clamp(settings.RefreshRateHz < 30 ? 120 : settings.RefreshRateHz, 30, 144),
            GridSnapPixels = Math.Clamp(settings.GridSnapPixels, 0, 50),
            FuelReserveLaps = Math.Clamp(settings.FuelReserveLaps, 0, 5),
            EnergyReservePercent = Math.Clamp(settings.EnergyReservePercent, 0, 25),
            ManualRemainingLaps = Math.Clamp(settings.ManualRemainingLaps, 0, 1000),
            ManualRemainingMinutes = Math.Clamp(settings.ManualRemainingMinutes, 0, 1440),
            ManualLapTimeSeconds = Math.Clamp(settings.ManualLapTimeSeconds, 0, 3600),
            ManualFuelPerLapLiters = Math.Clamp(settings.ManualFuelPerLapLiters, 0, 100),
            ManualFuelCapacityLiters = Math.Clamp(settings.ManualFuelCapacityLiters, 0, 1000),
            MaximumStintLaps = Math.Clamp(settings.MaximumStintLaps, 0, 1000),
            EstimatedPitLossSeconds = Math.Clamp(
                settings.EstimatedPitLossSeconds,
                0,
                600),
            AvailableTireSets = Math.Clamp(settings.AvailableTireSets, 0, 100),
            TireWearLimitPercent = Math.Clamp(settings.TireWearLimitPercent, 20, 95),
            EstimatedTireChangeSeconds = Math.Clamp(
                settings.EstimatedTireChangeSeconds,
                0,
                180),
            BackgroundOpacity = Math.Clamp(
                settings.BackgroundOpacity <= 0 ? 0.94 : settings.BackgroundOpacity,
                0.35,
                1),
            PedalHistorySeconds = Math.Clamp(
                settings.PedalHistorySeconds <= 0 ? 5 : settings.PedalHistorySeconds,
                3,
                10),
            RemoteDashboardPort = Math.Clamp(
                settings.RemoteDashboardPort <= 0 ? 28765 : settings.RemoteDashboardPort,
                1024,
                65535),
            RemoteDashboardToken = SanitizeRemoteDashboardToken(
                settings.RemoteDashboardToken),
        };
    }

    private static string SanitizeRemoteDashboardToken(string? value)
    {
        var token = new string((value ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Take(32)
            .ToArray());
        return token.Length >= 8
            ? token
            : RemoteDashboardDefaults.NewToken();
    }

    private static string SanitizeSteeringWheelImagePath(string? value)
    {
        var path = value?.Trim() ?? string.Empty;
        if (path.Length == 0)
        {
            return string.Empty;
        }
        return path.Length <= 1024 &&
               string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase)
            ? path
            : string.Empty;
    }

    private static string SanitizeDashboardTitle(string? value)
    {
        var title = string.IsNullOrWhiteSpace(value) ? "REDFOX RACING" : value.Trim();
        var printable = new string(title.Where(character => !char.IsControl(character)).ToArray());
        return printable.Length > 24 ? printable[..24] : printable;
    }

    private static WidgetPlacement SanitizePlacement(WidgetPlacement item) => item with
    {
        // A widget occupying 8% of one monitor can be below 3% of a triple-
        // monitor desktop. Preserve it; responsive layout enforces readability.
        X = Math.Clamp(item.X, 0, 0.999),
        Y = Math.Clamp(item.Y, 0, 0.999),
        Width = Math.Clamp(item.Width, 0.02, 1),
        Height = Math.Clamp(item.Height, 0.02, 1),
        Scale = Math.Clamp(item.Scale, 0.5, 2),
        Opacity = Math.Clamp(item.Opacity, 0.2, 1),
    };

    private sealed class LayoutCatalog
    {
        public LayoutCatalog(
            int schemaVersion,
            string activeProfile,
            Dictionary<string, LayoutProfile> profiles)
        {
            SchemaVersion = schemaVersion;
            ActiveProfile = activeProfile;
            Profiles = profiles;
        }

        public int SchemaVersion { get; }
        public string ActiveProfile { get; set; }
        public Dictionary<string, LayoutProfile> Profiles { get; }
    }

    private sealed record LayoutProfileExport(
        int FormatVersion,
        string Name,
        LayoutProfile Profile);
}
