using System.IO;
using System.Text.Json;
using LmuOverlay.Widgets;

namespace LmuOverlay.Desktop;

public sealed class SectorReferenceStore
{
    // Version 1 could contain sectors recorded with scoring-sector ordering
    // applied to the zero-based telemetry field. Do not reuse those records.
    private const int SchemaVersion = 2;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly object _sync = new();
    private readonly string _path;
    private Catalog? _catalog;

    public SectorReferenceStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LMU Overlay",
            "sector-references.json");
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
            var key = Key(trackName, vehicleModel);
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
            var key = Key(trackName, vehicleModel);
            if (catalog.Entries.TryGetValue(key, out var existing))
            {
                clean = Merge(existing.Reference, clean);
            }
            catalog.Entries[key] = new Entry(
                trackName.Trim(),
                vehicleModel.Trim(),
                clean,
                DateTimeOffset.UtcNow);
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
                if (catalog is { Version: SchemaVersion })
                {
                    return catalog;
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

    private static string Key(string trackName, string vehicleModel) =>
        $"{trackName.Trim().ToUpperInvariant()}\u001f" +
        vehicleModel.Trim().ToUpperInvariant();

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
        DateTimeOffset UpdatedAtUtc);

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
