namespace LmuOverlay.SteamVr;

public sealed record SteamVrOverlaySettings(
    float WidthMeters = 1.15f,
    float DistanceMeters = 1.35f,
    float VerticalOffsetMeters = -0.25f,
    float HorizontalOffsetMeters = 0,
    float Opacity = 0.96f)
{
    public SteamVrOverlaySettings Sanitize() => this with
    {
        WidthMeters = Math.Clamp(WidthMeters, 0.2f, 3f),
        DistanceMeters = Math.Clamp(DistanceMeters, 0.3f, 5f),
        VerticalOffsetMeters = Math.Clamp(VerticalOffsetMeters, -2f, 2f),
        HorizontalOffsetMeters = Math.Clamp(HorizontalOffsetMeters, -2f, 2f),
        Opacity = Math.Clamp(Opacity, 0.1f, 1f),
    };
}

public readonly record struct SteamVrMatrix34(
    float M0, float M1, float M2, float M3,
    float M4, float M5, float M6, float M7,
    float M8, float M9, float M10, float M11)
{
    public static SteamVrMatrix34 HeadLocked(SteamVrOverlaySettings settings)
    {
        var safe = settings.Sanitize();
        return new(
            1, 0, 0, safe.HorizontalOffsetMeters,
            0, 1, 0, safe.VerticalOffsetMeters,
            0, 0, 1, -safe.DistanceMeters);
    }
}
