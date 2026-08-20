using System.Text.Json;

namespace LmuOverlay.Widgets;

public readonly record struct TireTemperatureProfile(
    double ColdToWarming,
    double WarmingToOptimal,
    double OptimalToHot,
    double HotToCritical)
{
    public static TireTemperatureProfile Default => new(60, 75, 100, 115);

    public TireTemperatureProfile Sanitize() =>
        ColdToWarming is > 0 and < 200 &&
        WarmingToOptimal > ColdToWarming &&
        OptimalToHot > WarmingToOptimal &&
        HotToCritical > OptimalToHot && HotToCritical < 250
            ? this
            : Default;
}

public sealed record TireTemperatureProfileEntry(
    string VehicleClass,
    string VehicleModel,
    string Compound,
    TireTemperatureProfile Thresholds);

public static class TireTemperatureProfiles
{
    public const int SchemaVersion = 1;
    private static readonly object Sync = new();
    private static string? _path;
    private static DateTime _lastWrite;
    private static IReadOnlyList<TireTemperatureProfileEntry> _entries = [];

    public static void UseExternalCatalog(string? path)
    {
        lock (Sync)
        {
            _path = path;
            _lastWrite = DateTime.MinValue;
            _entries = [];
        }
    }

    public static TireTemperatureProfile Resolve(
        string vehicleClass,
        string vehicleModel,
        string compound)
    {
        var entries = Load();
        var match = entries
            .Where(entry => Matches(entry.VehicleClass, vehicleClass) &&
                Matches(entry.VehicleModel, vehicleModel) &&
                Matches(entry.Compound, compound))
            .OrderByDescending(Specificity)
            .FirstOrDefault();
        return (match?.Thresholds ?? TireTemperatureProfile.Default).Sanitize();
    }

    private static bool Matches(string configured, string actual) =>
        string.IsNullOrWhiteSpace(configured) || configured == "*" ||
        actual.Contains(configured, StringComparison.OrdinalIgnoreCase);

    private static int Specificity(TireTemperatureProfileEntry entry) =>
        new[] { entry.VehicleClass, entry.VehicleModel, entry.Compound }
            .Count(value => !string.IsNullOrWhiteSpace(value) && value != "*");

    private static IReadOnlyList<TireTemperatureProfileEntry> Load()
    {
        var path = _path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LMU Overlay", "tire-temperature-profiles.json");
        try
        {
            var write = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
            lock (Sync)
            {
                if (write == _lastWrite) return _entries;
                _lastWrite = write;
                if (write == DateTime.MinValue) return _entries = [];
                using var stream = File.OpenRead(path);
                var catalog = JsonSerializer.Deserialize<Catalog>(stream, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                });
                _entries = catalog is { Version: SchemaVersion }
                    ? catalog.Entries.ToArray()
                    : [];
                return _entries;
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return _entries;
        }
    }

    private sealed record Catalog(int Version, IReadOnlyList<TireTemperatureProfileEntry> Entries);
}
