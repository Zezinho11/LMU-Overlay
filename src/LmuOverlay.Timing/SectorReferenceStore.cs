using System.IO;
using System.Text.Json;

namespace LmuOverlay.Widgets;

public sealed class SectorReferenceStore
{
    // Versions 1-2 could contain swapped sectors or the cumulative S1+S2
    // scoring split stored as an individual S2. Rebuild from official timing.
    private const int SchemaVersion = 4;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly object _sync = new();
    private readonly string _path;
    private readonly string _generation;
    private Catalog? _catalog;

    public SectorReferenceStore(string? path = null, string compatibilityGeneration = "layout-v1")
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LMU Overlay",
            "sector-references.json");
        _generation = NormalizeGeneration(compatibilityGeneration);
    }

    public SectorReferenceSeed Load(string trackName, string vehicleModel)
    {
        if (string.IsNullOrWhiteSpace(trackName) ||
            string.IsNullOrWhiteSpace(vehicleModel))
        {
            return default;
        }

        lock (_sync)
        {
            var key = Key(trackName, vehicleModel, _generation);
            return EnsureCatalog().Entries.TryGetValue(key, out var entry)
                ? Sanitize(entry.Reference)
                : default;
        }
    }

    public void Save(
        string trackName,
        string vehicleModel,
        SectorReferenceSeed reference)
    {
        if (string.IsNullOrWhiteSpace(trackName) ||
            string.IsNullOrWhiteSpace(vehicleModel))
        {
            return;
        }

        var clean = Sanitize(reference);
        if (clean == default)
        {
            return;
        }

        lock (_sync)
        {
            var catalog = EnsureCatalog();
            var key = Key(trackName, vehicleModel, _generation);
            if (catalog.Entries.TryGetValue(key, out var existing))
            {
                clean = Merge(existing.Reference, clean);
            }
            catalog.Entries[key] = new Entry(
                trackName.Trim(),
                vehicleModel.Trim(),
                clean,
                DateTimeOffset.UtcNow,
                _generation);
            WriteAtomic(JsonSerializer.Serialize(catalog, Options));
        }
    }

    private Catalog EnsureCatalog() => _catalog ??= ReadCatalog();

    private Catalog ReadCatalog()
    {
        try
        {
            if (File.Exists(_path))
            {
                var catalog = JsonSerializer.Deserialize<Catalog>(
                    File.ReadAllText(_path),
                    Options);
                if (catalog is { Version: 3 or SchemaVersion })
                {
                    return catalog.Version == SchemaVersion
                        ? catalog
                        : new Catalog(SchemaVersion, catalog.Entries);
                }
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
        }

        return new Catalog(SchemaVersion, new(StringComparer.OrdinalIgnoreCase));
    }

    private void WriteAtomic(string contents)
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, contents);
            File.Move(temporary, _path, true);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // Sector persistence is an enhancement; live timing continues if
            // the local profile directory is temporarily unavailable.
        }
    }

    private static string Key(string trackName, string vehicleModel, string generation) =>
        $"{trackName.Trim().ToUpperInvariant()}\u001f" +
        $"{vehicleModel.Trim().ToUpperInvariant()}\u001f" +
        NormalizeGeneration(generation).ToUpperInvariant();

    private static string NormalizeGeneration(string value) =>
        string.IsNullOrWhiteSpace(value) ? "layout-v1" : value.Trim();

    private static SectorReferenceSeed Merge(
        SectorReferenceSeed first,
        SectorReferenceSeed second) => new(
        Better(first.Sector1Seconds, second.Sector1Seconds),
        Better(first.Sector2Seconds, second.Sector2Seconds),
        Better(first.Sector3Seconds, second.Sector3Seconds));

    private static double Better(double first, double second) =>
        Sanitize(first) switch
        {
            <= 0 => Sanitize(second),
            var cleanFirst when Sanitize(second) <= 0 => cleanFirst,
            var cleanFirst => Math.Min(cleanFirst, Sanitize(second)),
        };

    private static SectorReferenceSeed Sanitize(SectorReferenceSeed value) => new(
        Sanitize(value.Sector1Seconds),
        Sanitize(value.Sector2Seconds),
        Sanitize(value.Sector3Seconds));

    private static double Sanitize(double seconds) =>
        double.IsFinite(seconds) && seconds is > 1 and < 600 ? seconds : 0;

    private sealed record Entry(
        string TrackName,
        string VehicleModel,
        SectorReferenceSeed Reference,
        DateTimeOffset UpdatedAtUtc,
        string CompatibilityGeneration = "legacy");

    private sealed class Catalog
    {
        public Catalog(int version, Dictionary<string, Entry> entries)
        {
            Version = version;
            Entries = entries;
        }

        public int Version { get; }
        public Dictionary<string, Entry> Entries { get; }
    }
}
