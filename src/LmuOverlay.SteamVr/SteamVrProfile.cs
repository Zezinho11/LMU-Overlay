using System.Text.Json;

namespace LmuOverlay.SteamVr;

public sealed record SteamVrWidgetPlacement(
    bool Visible,
    float WidthMeters,
    float DistanceMeters,
    float VerticalOffsetMeters,
    float HorizontalOffsetMeters,
    float Opacity)
{
    public SteamVrOverlaySettings ToSettings() => new SteamVrOverlaySettings(
        WidthMeters,
        DistanceMeters,
        VerticalOffsetMeters,
        HorizontalOffsetMeters,
        Opacity).Sanitize();

    public SteamVrWidgetPlacement Sanitize()
    {
        var safe = ToSettings();
        return this with
        {
            WidthMeters = safe.WidthMeters,
            DistanceMeters = safe.DistanceMeters,
            VerticalOffsetMeters = safe.VerticalOffsetMeters,
            HorizontalOffsetMeters = safe.HorizontalOffsetMeters,
            Opacity = safe.Opacity,
        };
    }
}

public sealed record SteamVrProfile(
    int SchemaVersion,
    SteamVrWidgetPlacement Dashboard,
    SteamVrWidgetPlacement LiveStandings,
    SteamVrWidgetPlacement Relative,
    SteamVrWidgetPlacement FuelStrategy,
    SteamVrWidgetPlacement SessionFlags)
{
    public const int CurrentSchemaVersion = 1;

    public static SteamVrProfile Default => new(
        CurrentSchemaVersion,
        new(true, 1.05f, 1.45f, -0.32f, 0, 0.96f),
        new(true, 0.48f, 1.55f, 0.02f, -0.72f, 0.94f),
        new(true, 0.48f, 1.55f, 0.02f, 0.72f, 0.94f),
        new(true, 0.56f, 1.6f, -0.55f, 0.72f, 0.94f),
        new(true, 0.78f, 1.6f, 0.48f, 0, 0.92f));

    public static SteamVrProfile Compact => new(
        CurrentSchemaVersion,
        new(true, 0.88f, 1.5f, -0.28f, 0, 0.98f),
        new(false, 0.42f, 1.55f, 0, -0.65f, 0.95f),
        new(true, 0.40f, 1.5f, 0.02f, 0.60f, 0.96f),
        new(false, 0.50f, 1.6f, -0.5f, 0.65f, 0.94f),
        new(false, 0.65f, 1.6f, 0.44f, 0, 0.94f));

    public static SteamVrProfile Endurance => Default;

    public SteamVrProfile Calibrate(float widthScale, float distanceMeters)
    {
        var scale = Math.Clamp(widthScale, 0.6f, 1.8f);
        var distance = Math.Clamp(distanceMeters, 0.6f, 3f);
        SteamVrWidgetPlacement Update(SteamVrWidgetPlacement placement) =>
            placement with
            {
                WidthMeters = placement.WidthMeters * scale,
                DistanceMeters = distance,
            };
        return this with
        {
            Dashboard = Update(Dashboard),
            LiveStandings = Update(LiveStandings),
            Relative = Update(Relative),
            FuelStrategy = Update(FuelStrategy),
            SessionFlags = Update(SessionFlags),
        };
    }

    public SteamVrProfile Sanitize() => new(
        CurrentSchemaVersion,
        (Dashboard ?? Default.Dashboard).Sanitize(),
        (LiveStandings ?? Default.LiveStandings).Sanitize(),
        (Relative ?? Default.Relative).Sanitize(),
        (FuelStrategy ?? Default.FuelStrategy).Sanitize(),
        (SessionFlags ?? Default.SessionFlags).Sanitize());
}

public sealed class SteamVrProfileStore(string? path = null)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    public string Path { get; } = path ?? System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LMU Overlay",
        "steamvr-profile.json");

    public SteamVrProfile Load()
    {
        try
        {
            if (!File.Exists(Path))
            {
                var created = SteamVrProfile.Default;
                Save(created);
                return created;
            }

            var profile = JsonSerializer.Deserialize<SteamVrProfile>(
                File.ReadAllText(Path),
                Options);
            return profile?.Sanitize() ?? SteamVrProfile.Default;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return SteamVrProfile.Default;
        }
    }

    public void Save(SteamVrProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = Path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(profile.Sanitize(), Options));
        File.Move(temporary, Path, true);
    }
}
