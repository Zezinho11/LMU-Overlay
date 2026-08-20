namespace LmuOverlay.Desktop;

public enum OverlayDensity
{
    Auto,
    Compact,
    Normal,
    Expanded,
}

public static class ProfileValueSanitizer
{
    public static string NormalizeHexColor(string? value, string fallback)
    {
        var candidate = value?.Trim() ?? string.Empty;
        if (!candidate.StartsWith('#'))
        {
            candidate = $"#{candidate}";
        }
        return candidate.Length == 7 &&
               int.TryParse(
                   candidate.AsSpan(1),
                   System.Globalization.NumberStyles.HexNumber,
                   System.Globalization.CultureInfo.InvariantCulture,
                   out _)
            ? candidate.ToUpperInvariant()
            : fallback;
    }
}
