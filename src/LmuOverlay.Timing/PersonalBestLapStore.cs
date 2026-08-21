using System.IO;
using System.Text.Json;

namespace LmuOverlay.Widgets;

public sealed class PersonalBestLapStore
{
    // Versions 1-2 could contain reconstructed laps or a cumulative S1+S2
    // split persisted as S2. Start a clean official-only individual-split catalog.
    private const int SchemaVersion = 5;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly object _sync = new();
    private readonly string _path;
    private readonly string _generation;
    private Catalog? _catalog;

    public PersonalBestLapStore(string? path = null, string compatibilityGeneration = "layout-v1")
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LMU Overlay",
            "personal-bests.json");
        _generation = NormalizeGeneration(compatibilityGeneration);
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

        return LoadRecord(trackName, driverName, vehicleModel).BestLap;
    }

    public PersonalTimingRecord LoadRecord(
        string trackName,
        string driverName,
        string vehicleModel)
    {
        if (!ValidKeyPart(trackName) || !ValidKeyPart(driverName) || !ValidKeyPart(vehicleModel))
        {
            return default;
        }

        lock (_sync)
        {
            if (!EnsureCatalog().Entries.TryGetValue(
                    Key(trackName, driverName, vehicleModel, _generation), out var entry))
            {
                return default;
            }
            var bestLap = entry.BestLap.IsValid ? entry.BestLap : default;
            var sectors = entry.BestSectors == default && bestLap.IsValid
                ? bestLap.Sectors
                : Sanitize(entry.BestSectors);
            var optimal = ValidOptimal(entry.OptimalLapTimeSeconds)
                ? entry.OptimalLapTimeSeconds
                : sectors.Optimal;
            return new(bestLap, sectors, optimal);
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
            var key = Key(trackName, driverName, vehicleModel, _generation);
            if (catalog.Entries.TryGetValue(key, out var existing) &&
                existing.BestLap.IsValid &&
                existing.BestLap.LapTimeSeconds <= candidate.LapTimeSeconds)
            {
                return existing.BestLap;
            }

            var bestSectors = catalog.Entries.TryGetValue(key, out existing)
                ? Merge(existing.BestSectors, candidate.Sectors)
                : candidate.Sectors;
            var optimal = catalog.Entries.TryGetValue(key, out existing)
                ? Better(existing.OptimalLapTimeSeconds, bestSectors.Optimal)
                : bestSectors.Optimal;
            catalog.Entries[key] = new Entry(
                trackName.Trim(),
                driverName.Trim(),
                vehicleModel.Trim(),
                candidate,
                DateTimeOffset.UtcNow,
                bestSectors,
                optimal,
                _generation);
            WriteAtomic(JsonSerializer.Serialize(catalog, Options));
            return candidate;
        }
    }

    public SectorReferenceSeed SaveSectorsIfFaster(
        string trackName,
        string driverName,
        string vehicleModel,
        SectorReferenceSeed candidate)
    {
        var clean = Sanitize(candidate);
        if (!ValidKeyPart(trackName) || !ValidKeyPart(driverName) ||
            !ValidKeyPart(vehicleModel) || clean == default)
        {
            return LoadRecord(trackName, driverName, vehicleModel).BestSectors;
        }

        lock (_sync)
        {
            var catalog = EnsureCatalog();
            var key = Key(trackName, driverName, vehicleModel, _generation);
            catalog.Entries.TryGetValue(key, out var existing);
            var bestSectors = existing is null
                ? clean
                : Merge(existing.BestSectors, clean);
            var optimal = bestSectors.Optimal;
            catalog.Entries[key] = existing is null
                ? new Entry(
                    trackName.Trim(), driverName.Trim(), vehicleModel.Trim(), default,
                    DateTimeOffset.UtcNow, bestSectors, optimal, _generation)
                : existing with
                {
                    BestSectors = bestSectors,
                    OptimalLapTimeSeconds = optimal,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                };
            WriteAtomic(JsonSerializer.Serialize(catalog, Options));
            return bestSectors;
        }
    }

    public double SaveOptimalIfFaster(
        string trackName,
        string driverName,
        string vehicleModel,
        double optimalLapTimeSeconds)
    {
        if (!ValidKeyPart(trackName) || !ValidKeyPart(driverName) ||
            !ValidKeyPart(vehicleModel) || !ValidOptimal(optimalLapTimeSeconds))
        {
            return LoadRecord(trackName, driverName, vehicleModel).OptimalLapTimeSeconds;
        }

        lock (_sync)
        {
            var catalog = EnsureCatalog();
            var key = Key(trackName, driverName, vehicleModel, _generation);
            catalog.Entries.TryGetValue(key, out var existing);
            var best = Better(existing?.OptimalLapTimeSeconds ?? 0, optimalLapTimeSeconds);
            catalog.Entries[key] = existing is null
                ? new Entry(
                    trackName.Trim(), driverName.Trim(), vehicleModel.Trim(), default,
                    DateTimeOffset.UtcNow, default, best, _generation)
                : existing with { OptimalLapTimeSeconds = best, UpdatedAtUtc = DateTimeOffset.UtcNow };
            WriteAtomic(JsonSerializer.Serialize(catalog, Options));
            return best;
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
                if (catalog is { Version: 3 or 4 or SchemaVersion })
                {
                    return new Catalog(
                        SchemaVersion,
                        catalog.Entries.ToDictionary(
                            pair => pair.Key,
                            pair => pair.Value with
                            {
                                BestSectors = pair.Value.BestSectors == default && pair.Value.BestLap.IsValid
                                    ? pair.Value.BestLap.Sectors
                                    : Sanitize(pair.Value.BestSectors),
                            },
                            StringComparer.OrdinalIgnoreCase));
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

    private static bool ValidOptimal(double seconds) =>
        double.IsFinite(seconds) && seconds is > 10 and < 1_800;

    private static SectorReferenceSeed Merge(SectorReferenceSeed first, SectorReferenceSeed second) => new(
        Better(first.Sector1Seconds, second.Sector1Seconds),
        Better(first.Sector2Seconds, second.Sector2Seconds),
        Better(first.Sector3Seconds, second.Sector3Seconds));

    private static SectorReferenceSeed Sanitize(SectorReferenceSeed value) => new(
        ValidSector(value.Sector1Seconds) ? value.Sector1Seconds : 0,
        ValidSector(value.Sector2Seconds) ? value.Sector2Seconds : 0,
        ValidSector(value.Sector3Seconds) ? value.Sector3Seconds : 0);

    private static bool ValidSector(double seconds) =>
        double.IsFinite(seconds) && seconds is > 1 and < 600;

    private static double Better(double first, double second) =>
        first <= 0 ? second : second <= 0 ? first : Math.Min(first, second);

    private static string Key(
        string trackName,
        string driverName,
        string vehicleModel,
        string generation) =>
        $"{trackName.Trim().ToUpperInvariant()}\u001f" +
        $"{driverName.Trim().ToUpperInvariant()}\u001f" +
        $"{vehicleModel.Trim().ToUpperInvariant()}\u001f" +
        NormalizeGeneration(generation).ToUpperInvariant();

    private static string NormalizeGeneration(string value) =>
        string.IsNullOrWhiteSpace(value) ? "layout-v1" : value.Trim();

    private sealed record Entry(
        string TrackName,
        string DriverName,
        string VehicleModel,
        PersonalBestLap BestLap,
        DateTimeOffset UpdatedAtUtc,
        SectorReferenceSeed BestSectors = default,
        double OptimalLapTimeSeconds = 0,
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

public readonly record struct PersonalTimingRecord(
    PersonalBestLap BestLap,
    SectorReferenceSeed BestSectors,
    double OptimalLapTimeSeconds);
