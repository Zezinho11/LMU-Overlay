using System.Reflection;
using System.Text.Json;

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
    private static readonly Lazy<Catalog> Data = new(Load, true);

    public static VehicleIdentity Resolve(string vehicleModel, string vehicleName = "")
    {
        var searchable = $"{vehicleModel} {vehicleName}".Trim();
        var match = Data.Value.Entries.FirstOrDefault(entry => entry.Tokens.Any(token =>
            searchable.Contains(token, StringComparison.OrdinalIgnoreCase)));
        return match is not null
            ? new(match.Manufacturer, match.Code, NormalizeColor(match.Color), true)
            : new("Unknown", "---", "#697784", false);
    }

    public static IReadOnlyList<VehicleCatalogEntry> Entries => Data.Value.Entries;

    private static Catalog Load()
    {
        var assembly = typeof(VehicleCatalog).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(name =>
            name.EndsWith("vehicle-catalog.v1.json", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException("Embedded vehicle catalog is missing.");
        var catalog = JsonSerializer.Deserialize<Catalog>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw new InvalidDataException("Vehicle catalog is invalid.");
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

    private static string NormalizeColor(string color) =>
        color.Length == 7 && color[0] == '#' ? color.ToUpperInvariant() : "#697784";

    private sealed record Catalog(int SchemaVersion, IReadOnlyList<VehicleCatalogEntry> Entries);
}
