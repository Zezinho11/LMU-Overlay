namespace LmuOverlay.Widgets;

public enum DashboardModule
{
    Sectors,
    Tires,
    Telemetry,
}

public static class DashboardModuleLayout
{
    public const string DefaultOrder = "Sectors,Tires,Telemetry";

    public static IReadOnlyList<DashboardModule> Parse(string? value)
    {
        var parsed = (value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => Enum.TryParse<DashboardModule>(item, true, out var module)
                ? (DashboardModule?)module
                : null)
            .Where(module => module.HasValue)
            .Select(module => module!.Value)
            .Distinct()
            .ToList();
        foreach (var module in Enum.GetValues<DashboardModule>())
        {
            if (!parsed.Contains(module)) parsed.Add(module);
        }
        return parsed;
    }

    public static string Normalize(string? value) => string.Join(',', Parse(value));
}
