using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;

namespace LmuOverlay.Desktop;

public enum OverlayDensity
{
    Auto,
    Compact,
    Normal,
    Expanded,
}

public enum OverlayAlertSeverity
{
    Information,
    Attention,
    Critical,
}

public sealed record OverlayThemePalette(
    Color Background,
    Color Card,
    Color Accent,
    Color PrimaryText,
    Color SecondaryText,
    Color Information,
    Color Attention,
    Color Critical,
    Color Positive);

public static class OverlayVisualSystem
{
    public static OverlayThemePalette Resolve(string theme) => theme switch
    {
        "HighContrast" => new(
            Colors.Black,
            Color.FromRgb(10, 10, 10),
            Colors.White,
            Colors.White,
            Color.FromRgb(220, 220, 220),
            Color.FromRgb(70, 205, 255),
            Color.FromRgb(255, 215, 0),
            Color.FromRgb(255, 70, 70),
            Color.FromRgb(70, 255, 130)),
        "Black" => new(
            Color.FromRgb(2, 3, 4),
            Color.FromRgb(12, 15, 18),
            Color.FromRgb(112, 124, 136),
            Colors.White,
            Color.FromRgb(190, 198, 206),
            Color.FromRgb(65, 185, 240),
            Color.FromRgb(255, 190, 64),
            Color.FromRgb(255, 70, 75),
            Color.FromRgb(66, 211, 166)),
        _ => new(
            Color.FromRgb(10, 15, 26),
            Color.FromRgb(18, 25, 36),
            Color.FromRgb(66, 211, 166),
            Colors.White,
            Color.FromRgb(202, 211, 220),
            Color.FromRgb(18, 217, 229),
            Color.FromRgb(255, 190, 64),
            Color.FromRgb(255, 70, 75),
            Color.FromRgb(66, 211, 166)),
    };

    public static OverlayDensity ResolveDensity(
        string requested,
        double renderedWidth,
        double designWidth)
    {
        if (Enum.TryParse<OverlayDensity>(requested, true, out var explicitDensity) &&
            explicitDensity != OverlayDensity.Auto)
        {
            return explicitDensity;
        }

        var scale = designWidth > 0 ? renderedWidth / designWidth : 1;
        return scale switch
        {
            < 0.72 => OverlayDensity.Compact,
            > 1.15 => OverlayDensity.Expanded,
            _ => OverlayDensity.Normal,
        };
    }

    public static double ContrastRatio(Color foreground, Color background)
    {
        var lighter = Math.Max(Luminance(foreground), Luminance(background));
        var darker = Math.Min(Luminance(foreground), Luminance(background));
        return (lighter + 0.05) / (darker + 0.05);
    }

    public static Color WithOpacity(Color color, double opacity) => Color.FromArgb(
        (byte)Math.Round(Math.Clamp(opacity, 0, 1) * color.A),
        color.R,
        color.G,
        color.B);

    private static double Luminance(Color color) =>
        0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);

    private static double Linear(byte component)
    {
        var value = component / 255d;
        return value <= 0.04045
            ? value / 12.92
            : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
