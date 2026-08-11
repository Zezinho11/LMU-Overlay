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
    public static OverlayThemePalette Resolve(OverlayProfileSettings settings)
    {
        if (!string.Equals(settings.Theme, "Custom", StringComparison.Ordinal))
        {
            return Resolve(settings.Theme);
        }

        var background = ParseColor(settings.CustomBackgroundColor, Color.FromRgb(10, 15, 26));
        var primaryText = ContrastRatio(Colors.White, background) >= 4.5
            ? Colors.White
            : Colors.Black;
        var secondaryText = primaryText == Colors.White
            ? Color.FromRgb(202, 211, 220)
            : Color.FromRgb(45, 53, 61);
        var accent = ParseColor(settings.CustomAccentColor, Color.FromRgb(66, 211, 166));
        if (ContrastRatio(accent, background) < 2.5)
        {
            accent = primaryText == Colors.White
                ? Color.FromRgb(66, 211, 166)
                : Color.FromRgb(0, 92, 70);
        }

        return new(
            background,
            Blend(background, primaryText, 0.07),
            accent,
            primaryText,
            secondaryText,
            Color.FromRgb(18, 217, 229),
            Color.FromRgb(255, 190, 64),
            Color.FromRgb(255, 70, 75),
            Color.FromRgb(66, 211, 166));
    }

    public static OverlayThemePalette Resolve(string theme) => theme switch
    {
        "ColorVisionSafe" => new(
            Color.FromRgb(8, 14, 24),
            Color.FromRgb(18, 28, 42),
            Color.FromRgb(86, 180, 233),
            Colors.White,
            Color.FromRgb(210, 218, 226),
            Color.FromRgb(0, 114, 178),
            Color.FromRgb(230, 159, 0),
            Color.FromRgb(213, 94, 0),
            Color.FromRgb(0, 158, 115)),
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

    public static string NormalizeHexColor(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var candidate = value.Trim();
        if (!candidate.StartsWith('#'))
        {
            candidate = $"#{candidate}";
        }

        try
        {
            _ = (Color)System.Windows.Media.ColorConverter.ConvertFromString(candidate)!;
            return candidate.Length == 7 ? candidate.ToUpperInvariant() : fallback;
        }
        catch (FormatException)
        {
            return fallback;
        }
    }

    private static Color ParseColor(string value, Color fallback)
    {
        try
        {
            return (Color)System.Windows.Media.ColorConverter.ConvertFromString(value)!;
        }
        catch (FormatException)
        {
            return fallback;
        }
    }

    private static Color Blend(Color first, Color second, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return Color.FromRgb(
            (byte)Math.Round(first.R + ((second.R - first.R) * amount)),
            (byte)Math.Round(first.G + ((second.G - first.G) * amount)),
            (byte)Math.Round(first.B + ((second.B - first.B) * amount)));
    }

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
