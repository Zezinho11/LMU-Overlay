namespace LmuOverlay.DirectX;

public readonly record struct NativeOverlayColor(byte Red, byte Green, byte Blue);

public sealed record NativeOverlayStyle(
    NativeOverlayColor Background,
    NativeOverlayColor Card,
    NativeOverlayColor Accent,
    NativeOverlayColor PrimaryText,
    NativeOverlayColor SecondaryText,
    NativeOverlayColor Information,
    NativeOverlayColor Attention,
    NativeOverlayColor Critical,
    NativeOverlayColor Positive,
    double BackgroundOpacity,
    string DashboardTitle,
    double DashboardTextScale,
    double TimingTextScale,
    double InputsTextScale)
{
    public static NativeOverlayStyle RedFox { get; } = new(
        new(10, 15, 26),
        new(18, 25, 36),
        new(66, 211, 166),
        new(255, 255, 255),
        new(202, 211, 220),
        new(18, 217, 229),
        new(255, 190, 64),
        new(255, 70, 75),
        new(66, 211, 166),
        0.94,
        "REDFOX RACING",
        1,
        1,
        1);
}
