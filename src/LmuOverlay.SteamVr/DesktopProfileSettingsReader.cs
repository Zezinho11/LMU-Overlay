using System.Text.Json;
using LmuOverlay.Desktop;
using LmuOverlay.Widgets;

namespace LmuOverlay.SteamVr;

public static class VrProfileSettingsExtensions
{
    public static FuelStrategyOptions FuelOptions(this OverlayProfileSettings settings) => new(
        settings.FuelReserveLaps,
        settings.EnergyReservePercent / 100,
        settings.ManualRemainingLaps,
        settings.MaximumStintLaps,
        settings.EstimatedPitLossSeconds,
        settings.AvailableTireSets,
        settings.TireWearLimitPercent / 100,
        settings.EstimatedTireChangeSeconds,
        settings.ManualRemainingMinutes,
        settings.ManualLapTimeSeconds,
        settings.ManualFuelPerLapLiters,
        settings.ManualFuelCapacityLiters);
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

    public OverlayProfileSettings Load()
    {
        try
        {
            if (!File.Exists(Path))
            {
                return new OverlayProfileSettings();
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

        return new OverlayProfileSettings();
    }

    private static OverlayProfileSettings SettingsFromProfile(JsonElement profile)
    {
        if (!Property(profile, "Settings", out var settings))
        {
            return new OverlayProfileSettings();
        }

        return LayoutStore.SanitizeSettings(
            settings.Deserialize<OverlayProfileSettings>(Options));
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
