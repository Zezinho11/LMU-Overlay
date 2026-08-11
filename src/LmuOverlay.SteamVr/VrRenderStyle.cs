using System.Drawing;

namespace LmuOverlay.SteamVr;

public sealed record VrRenderStyle(
    Color Background,
    Color Card,
    Color Accent,
    Color PrimaryText,
    Color SecondaryText,
    Color Information,
    Color Attention,
    Color Critical,
    Color Positive,
    string DashboardTitle,
    float DashboardTextScale,
    float TimingTextScale,
    float InputsTextScale)
{
    public static VrRenderStyle From(VrDesktopSettings settings)
    {
        settings = settings.Sanitize();
        var opacity = settings.Theme == "HighContrast"
            ? 1
            : settings.BackgroundOpacity;
        var palette = settings.Theme switch
        {
            "HighContrast" => Palette(
                Color.Black,
                Color.FromArgb(10, 10, 10),
                Color.White,
                Color.White,
                Color.FromArgb(220, 220, 220)),
            "Black" => Palette(
                Color.FromArgb(2, 3, 4),
                Color.FromArgb(12, 15, 18),
                Color.FromArgb(112, 124, 136),
                Color.White,
                Color.FromArgb(190, 198, 206)),
            "Custom" => Custom(settings),
            _ => Palette(
                Color.FromArgb(10, 15, 26),
                Color.FromArgb(18, 25, 36),
                Color.FromArgb(66, 211, 166),
                Color.White,
                Color.FromArgb(202, 211, 220)),
        };

        return new(
            Color.FromArgb((int)Math.Round(opacity * 255), palette.Background),
            Color.FromArgb((int)Math.Round(opacity * 255), palette.Card),
            palette.Accent,
            palette.Primary,
            palette.Secondary,
            Color.FromArgb(18, 217, 229),
            Color.FromArgb(255, 190, 64),
            Color.FromArgb(255, 70, 75),
            Color.FromArgb(66, 211, 166),
            settings.DashboardTitle,
            (float)settings.DashboardTextScale,
            (float)settings.TimingTextScale,
            (float)settings.InputsTextScale);
    }

    public static string NormalizeHex(string? value, string fallback)
    {
        var candidate = value?.Trim() ?? string.Empty;
        if (!candidate.StartsWith('#')) candidate = $"#{candidate}";
        if (candidate.Length != 7 ||
            !int.TryParse(candidate.AsSpan(1),
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture,
                out _))
        {
            return fallback;
        }
        return candidate.ToUpperInvariant();
    }

    private static VrPalette Custom(VrDesktopSettings settings)
    {
        var background = Parse(settings.CustomBackgroundColor, Color.FromArgb(10, 15, 26));
        var primary = Contrast(Color.White, background) >= 4.5 ? Color.White : Color.Black;
        var secondary = primary == Color.White
            ? Color.FromArgb(202, 211, 220)
            : Color.FromArgb(45, 53, 61);
        var accent = Parse(settings.CustomAccentColor, Color.FromArgb(66, 211, 166));
        if (Contrast(accent, background) < 2.5)
        {
            accent = primary == Color.White
                ? Color.FromArgb(66, 211, 166)
                : Color.FromArgb(0, 92, 70);
        }
        return Palette(background, Blend(background, primary, 0.07), accent, primary, secondary);
    }

    private static VrPalette Palette(
        Color background,
        Color card,
        Color accent,
        Color primary,
        Color secondary) => new(background, card, accent, primary, secondary);

    private static Color Parse(string value, Color fallback)
    {
        try { return ColorTranslator.FromHtml(value); }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            return fallback;
        }
    }

    private static Color Blend(Color first, Color second, double amount) => Color.FromArgb(
        (int)Math.Round(first.R + ((second.R - first.R) * amount)),
        (int)Math.Round(first.G + ((second.G - first.G) * amount)),
        (int)Math.Round(first.B + ((second.B - first.B) * amount)));

    private static double Contrast(Color first, Color second)
    {
        var lighter = Math.Max(Luminance(first), Luminance(second));
        var darker = Math.Min(Luminance(first), Luminance(second));
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double Luminance(Color color) =>
        0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);

    private static double Linear(byte value)
    {
        var component = value / 255d;
        return component <= 0.04045
            ? component / 12.92
            : Math.Pow((component + 0.055) / 1.055, 2.4);
    }

    private sealed record VrPalette(
        Color Background,
        Color Card,
        Color Accent,
        Color Primary,
        Color Secondary);
}
