using System.IO;
using System.Text.Json;
using LmuOverlay.Widgets;

namespace LmuOverlay.Widgets;

public sealed class PersonalBestLapStore
{
    // Versions 1-2 could contain reconstructed laps or a cumulative S1+S2
    // split persisted as S2. Start a clean official-only individual-split catalog.
    private const int SchemaVersion = 3;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly object _sync = new();
    private readonly string _path;
    private Catalog? _catalog;

    public PersonalBestLapStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LMU Overlay",
            "personal-bests.json");
    }

    public PersonalBestLap Load(
        string trackName,
        string driverName,
        string vehicleModel)
    {
        if (!ValidKeyPart(trackName) ||
            !ValidKeyPart(driverName) ||
            !ValidKeyPart(vehicleModel))
        {
            return default;
        }

        lock (_sync)
        {
            return EnsureCatalog().Entries.TryGetValue(
                Key(trackName, driverName, vehicleModel),
                out var entry) && entry.BestLap.IsValid
                    ? entry.BestLap
                    : default;
        }
    }

    public PersonalBestLap SaveIfFaster(
        string trackName,
        string driverName,
        string vehicleModel,
        PersonalBestLap candidate)
    {
        if (!ValidKeyPart(trackName) ||
            !ValidKeyPart(driverName) ||
            !ValidKeyPart(vehicleModel) ||
            !candidate.IsValid)
        {
            return Load(trackName, driverName, vehicleModel);
        }

        lock (_sync)
        {
            var catalog = EnsureCatalog();
            var key = Key(trackName, driverName, vehicleModel);
            if (catalog.Entries.TryGetValue(key, out var existing) &&
                existing.BestLap.IsValid &&
                existing.BestLap.LapTimeSeconds <= candidate.LapTimeSeconds)
            {
                return existing.BestLap;
            }

            catalog.Entries[key] = new Entry(
                trackName.Trim(),
                driverName.Trim(),
                vehicleModel.Trim(),
                candidate,
                DateTimeOffset.UtcNow);
            WriteAtomic(JsonSerializer.Serialize(catalog, Options));
            return candidate;
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
            // A transient profile write failure must not interrupt telemetry.
        }
    }

    private static bool ValidKeyPart(string value) =>
        !string.IsNullOrWhiteSpace(value);

    private static string Key(
        string trackName,
        string driverName,
        string vehicleModel) =>
        $"{trackName.Trim().ToUpperInvariant()}\u001f" +
        $"{driverName.Trim().ToUpperInvariant()}\u001f" +
        vehicleModel.Trim().ToUpperInvariant();

    private sealed record Entry(
        string TrackName,
        string DriverName,
        string VehicleModel,
        PersonalBestLap BestLap,
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
