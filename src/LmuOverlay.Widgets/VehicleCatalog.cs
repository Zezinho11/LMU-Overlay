using System.Reflection;
using System.Text.Json;
using System.Collections.Concurrent;

namespace LmuOverlay.Widgets;

public sealed record VehicleCatalogEntry(
    IReadOnlyList<string> Tokens,
    string Manufacturer,
    string Code,
    string Color);

public sealed record VehicleIdentity(
    string Manufacturer,
    string Code,
    string Color,
    bool IsCatalogMatch);

public static class VehicleCatalog
{
    public const int SupportedSchemaVersion = 1;
    private static readonly Catalog BuiltIn = Load();
    private static readonly object ExternalSync = new();
    private static readonly ConcurrentDictionary<string, byte> Unknown =
        new(StringComparer.OrdinalIgnoreCase);
    private static string? _externalPath;
    private static DateTime _externalWriteUtc;
    private static IReadOnlyList<VehicleCatalogEntry> _external = [];

    public static VehicleIdentity Resolve(string vehicleModel, string vehicleName = "")
    {
        var searchable = $"{vehicleModel} {vehicleName}".Trim();
        var match = Entries.FirstOrDefault(entry => entry.Tokens.Any(token =>
            searchable.Contains(token, StringComparison.OrdinalIgnoreCase)));
        if (match is null && searchable.Length > 0 && Unknown.TryAdd(searchable, 0))
        {
            TryAppendUnknown(searchable);
        }
        return match is not null
            ? new(match.Manufacturer, match.Code, NormalizeColor(match.Color), true)
            : new("Unknown", "---", "#697784", false);
    }

    public static IReadOnlyList<VehicleCatalogEntry> Entries =>
        ExternalEntries().Concat(BuiltIn.Entries).ToArray();

    public static IReadOnlyList<string> UnknownVehicles => Unknown.Keys.Order().ToArray();

    public static void UseExternalCatalog(string? path)
    {
        lock (ExternalSync)
        {
            _externalPath = path;
            _externalWriteUtc = DateTime.MinValue;
            _external = [];
        }
    }

    private static IReadOnlyList<VehicleCatalogEntry> ExternalEntries()
    {
        var path = _externalPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LMU Overlay", "vehicle-catalog.json");
        try
        {
            var write = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
            lock (ExternalSync)
            {
                if (write == _externalWriteUtc) return _external;
                _externalWriteUtc = write;
                _external = write == DateTime.MinValue ? [] : ReadCatalog(path).Entries;
                return _external;
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return _external;
        }
    }

    private static Catalog ReadCatalog(string path)
    {
        using var stream = File.OpenRead(path);
        var catalog = JsonSerializer.Deserialize<Catalog>(stream, JsonOptions())
            ?? throw new InvalidDataException("Vehicle catalog is invalid.");
        return Sanitize(catalog);
    }

    private static Catalog Load()
    {
        var assembly = typeof(VehicleCatalog).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(name =>
            name.EndsWith("vehicle-catalog.v1.json", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException("Embedded vehicle catalog is missing.");
        var catalog = JsonSerializer.Deserialize<Catalog>(stream, JsonOptions())
            ?? throw new InvalidDataException("Vehicle catalog is invalid.");
        return Sanitize(catalog);
    }

    private static Catalog Sanitize(Catalog catalog)
    {
        if (catalog.SchemaVersion != SupportedSchemaVersion || catalog.Entries.Count == 0)
        {
            throw new InvalidDataException("Vehicle catalog schema is unsupported.");
        }
        return catalog with
        {
            Entries = catalog.Entries
                .Where(entry => entry.Tokens.Count > 0 && entry.Code.Length == 3)
                .ToArray(),
        };
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static void TryAppendUnknown(string searchable)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LMU Overlay", "diagnostics");
            Directory.CreateDirectory(directory);
            File.AppendAllLines(Path.Combine(directory, "unknown-vehicles.txt"),
                [$"{DateTimeOffset.UtcNow:O}\t{searchable.Replace('\t', ' ')}"]);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static string NormalizeColor(string color) =>
        color.Length == 7 && color[0] == '#' ? color.ToUpperInvariant() : "#697784";

    private sealed record Catalog(int SchemaVersion, IReadOnlyList<VehicleCatalogEntry> Entries);
}
