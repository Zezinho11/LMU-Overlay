using System.IO;
using System.Text.Json;

namespace LmuOverlay.Configuration;

public sealed partial class LayoutStore
{
    private LayoutCatalog EnsureCatalog()
    {
        if (_catalog is not null)
        {
            return _catalog;
        }

        _catalog = ReadCatalog();
        return _catalog;
    }

    private LayoutCatalog ReadCatalog()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return CreateDefaultCatalog();
            }

            var json = File.ReadAllText(_path);
            var catalog = JsonSerializer.Deserialize<LayoutCatalog>(json, JsonOptions);
            if (catalog is { SchemaVersion: CatalogSchemaVersion } &&
                catalog.Profiles.Count > 0)
            {
                var sanitizedProfiles = catalog.Profiles.ToDictionary(
                    pair => pair.Key,
                    pair => Sanitize(pair.Value),
                    StringComparer.OrdinalIgnoreCase);
                var active = sanitizedProfiles.Keys.FirstOrDefault(name =>
                    string.Equals(name, catalog.ActiveProfile, StringComparison.OrdinalIgnoreCase))
                    ?? sanitizedProfiles.Keys.First();
                return new LayoutCatalog(CatalogSchemaVersion, active, sanitizedProfiles);
            }

            var legacy = JsonSerializer.Deserialize<LayoutProfile>(json, JsonOptions);
            if (legacy is { SchemaVersion: >= 1 and <= LayoutProfile.CurrentSchemaVersion })
            {
                return new LayoutCatalog(
                    CatalogSchemaVersion,
                    DefaultProfileName,
                    new Dictionary<string, LayoutProfile>(StringComparer.OrdinalIgnoreCase)
                    {
                        [DefaultProfileName] = Sanitize(legacy),
                    });
            }
        }
        catch (JsonException)
        {
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return CreateDefaultCatalog();
    }

    private void SaveCatalog(LayoutCatalog catalog) =>
        WriteAtomic(_path, JsonSerializer.Serialize(catalog, JsonOptions));

    private static void WriteAtomic(string path, string contents)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, contents);
        File.Move(temporaryPath, path, true);
    }
}
