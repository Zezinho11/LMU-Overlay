using System.Text.Json;
using LmuOverlay.Widgets;

namespace LmuOverlay.SteamVr;

public sealed record VrDesktopSettings
{
    public string Language { get; init; } = OverlayText.PortugueseBrazil;
    public string Theme { get; init; } = "RedFox";
    public string CustomAccentColor { get; init; } = "#42D3A6";
    public string CustomBackgroundColor { get; init; } = "#0A0F1A";
    public string DashboardTitle { get; init; } = "REDFOX RACING";
    public double DashboardTextScale { get; init; } = 1;
    public double TimingTextScale { get; init; } = 1;
    public double InputsTextScale { get; init; } = 1;
    public int LiveStandingsMaximumRows { get; init; } = 12;
    public int RelativeCarsEachSide { get; init; } = 4;
    public int RefreshRateHz { get; init; } = 120;
    public double FuelReserveLaps { get; init; } = 1;
    public double EnergyReservePercent { get; init; } = 2;
    public int ManualRemainingLaps { get; init; }
    public double ManualRemainingMinutes { get; init; }
    public double ManualLapTimeSeconds { get; init; }
    public double ManualFuelPerLapLiters { get; init; }
    public double ManualFuelCapacityLiters { get; init; }
    public int MaximumStintLaps { get; init; }
    public double EstimatedPitLossSeconds { get; init; } = 30;
    public int AvailableTireSets { get; init; }
    public double TireWearLimitPercent { get; init; } = 70;
    public double EstimatedTireChangeSeconds { get; init; } = 15;
    public double BackgroundOpacity { get; init; } = 0.94;
    public int PedalHistorySeconds { get; init; } = 5;
    public bool ShowPriorityAlerts { get; init; } = true;
    public bool ReduceMotion { get; init; } = true;

    public VrDesktopSettings Sanitize() => this with
    {
        Language = OverlayText.Normalize(Language),
        Theme = Theme is "RedFox" or "Black" or "HighContrast" or "ColorVisionSafe" or "Custom"
            ? Theme
            : "RedFox",
        CustomAccentColor = VrRenderStyle.NormalizeHex(CustomAccentColor, "#42D3A6"),
        CustomBackgroundColor = VrRenderStyle.NormalizeHex(CustomBackgroundColor, "#0A0F1A"),
        DashboardTitle = string.IsNullOrWhiteSpace(DashboardTitle)
            ? "REDFOX RACING"
            : new string(DashboardTitle.Trim().Where(value => !char.IsControl(value)).Take(24).ToArray()),
        DashboardTextScale = Math.Clamp(DashboardTextScale <= 0 ? 1 : DashboardTextScale, 0.8, 1.25),
        TimingTextScale = Math.Clamp(TimingTextScale <= 0 ? 1 : TimingTextScale, 0.8, 1.25),
        InputsTextScale = Math.Clamp(InputsTextScale <= 0 ? 1 : InputsTextScale, 0.8, 1.25),
        LiveStandingsMaximumRows = Math.Clamp(LiveStandingsMaximumRows <= 0 ? 12 : LiveStandingsMaximumRows, 6, 12),
        RelativeCarsEachSide = Math.Clamp(RelativeCarsEachSide <= 0 ? 4 : RelativeCarsEachSide, 2, 5),
        RefreshRateHz = Math.Clamp(RefreshRateHz <= 0 ? 120 : RefreshRateHz, 60, 120),
        FuelReserveLaps = Math.Clamp(FuelReserveLaps, 0, 5),
        EnergyReservePercent = Math.Clamp(EnergyReservePercent, 0, 25),
        BackgroundOpacity = Math.Clamp(BackgroundOpacity <= 0 ? 0.94 : BackgroundOpacity, 0.35, 1),
        PedalHistorySeconds = Math.Clamp(PedalHistorySeconds <= 0 ? 5 : PedalHistorySeconds, 3, 10),
    };

    public FuelStrategyOptions FuelOptions() => new(
        FuelReserveLaps,
        EnergyReservePercent / 100,
        ManualRemainingLaps,
        MaximumStintLaps,
        EstimatedPitLossSeconds,
        AvailableTireSets,
        TireWearLimitPercent / 100,
        EstimatedTireChangeSeconds,
        ManualRemainingMinutes,
        ManualLapTimeSeconds,
        ManualFuelPerLapLiters,
        ManualFuelCapacityLiters);
}

public sealed class DesktopProfileSettingsReader(string? path = null)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public string Path { get; } = path ?? System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LMU Overlay",
        "layout.json");

    public VrDesktopSettings Load()
    {
        try
        {
            if (!File.Exists(Path))
            {
                return new VrDesktopSettings();
            }

            using var document = JsonDocument.Parse(File.ReadAllText(Path));
            var root = document.RootElement;
            if (!Property(root, "ActiveProfile", out var activeElement) ||
                activeElement.ValueKind != JsonValueKind.String ||
                !Property(root, "Profiles", out var profiles) ||
                profiles.ValueKind != JsonValueKind.Object)
            {
                return SettingsFromProfile(root);
            }

            var active = activeElement.GetString();
            foreach (var profile in profiles.EnumerateObject())
            {
                if (string.Equals(profile.Name, active, StringComparison.OrdinalIgnoreCase))
                {
                    return SettingsFromProfile(profile.Value);
                }
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
        }

        return new VrDesktopSettings();
    }

    private static VrDesktopSettings SettingsFromProfile(JsonElement profile)
    {
        if (!Property(profile, "Settings", out var settings))
        {
            return new VrDesktopSettings();
        }

        return (settings.Deserialize<VrDesktopSettings>(Options) ?? new()).Sanitize();
    }

    private static bool Property(JsonElement value, string name, out JsonElement result)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in value.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    result = property.Value;
                    return true;
                }
            }
        }

        result = default;
        return false;
    }
}
