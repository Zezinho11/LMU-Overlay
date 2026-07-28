using System.IO;
using System.Text.Json;

namespace LmuOverlay.Desktop;

public sealed class LayoutStore
{
    public const string DefaultProfileName = "Padrão";
    private const int CatalogSchemaVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly object _sync = new();
    private readonly string _path;
    private LayoutCatalog? _catalog;

    public LayoutStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LMU Overlay",
            "layout.json");
    }

    public string ActiveProfileName
    {
        get
        {
            lock (_sync)
            {
                return EnsureCatalog().ActiveProfile;
            }
        }
    }

    public IReadOnlyList<string> ProfileNames
    {
        get
        {
            lock (_sync)
            {
                return EnsureCatalog().Profiles.Keys
                    .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                    .ToArray();
            }
        }
    }

    public LayoutProfile Load()
    {
        lock (_sync)
        {
            var catalog = EnsureCatalog();
            return catalog.Profiles[catalog.ActiveProfile];
        }
    }

    public void Save(LayoutProfile profile)
    {
        lock (_sync)
        {
            var catalog = EnsureCatalog();
            catalog.Profiles[catalog.ActiveProfile] = Sanitize(profile);
            SaveCatalog(catalog);
        }
    }

    public LayoutProfile Switch(string name)
    {
        lock (_sync)
        {
            var catalog = EnsureCatalog();
            var resolvedName = ResolveExistingName(catalog, name);
            catalog.ActiveProfile = resolvedName;
            SaveCatalog(catalog);
            return catalog.Profiles[resolvedName];
        }
    }

    public LayoutProfile Create(string name, LayoutProfile? source = null)
    {
        lock (_sync)
        {
            var catalog = EnsureCatalog();
            var cleanName = ValidateNewName(catalog, name);
            catalog.Profiles.Add(cleanName, Sanitize(source ?? LayoutProfile.Default));
            catalog.ActiveProfile = cleanName;
            SaveCatalog(catalog);
            return catalog.Profiles[cleanName];
        }
    }

    public string Rename(string currentName, string newName)
    {
        lock (_sync)
        {
            var catalog = EnsureCatalog();
            var resolvedCurrent = ResolveExistingName(catalog, currentName);
            var cleanName = NormalizeName(newName);
            if (!string.Equals(resolvedCurrent, cleanName, StringComparison.OrdinalIgnoreCase) &&
                catalog.Profiles.ContainsKey(cleanName))
            {
                throw new InvalidOperationException($"Já existe um perfil chamado '{cleanName}'.");
            }

            if (resolvedCurrent == cleanName)
            {
                return cleanName;
            }

            var profile = catalog.Profiles[resolvedCurrent];
            catalog.Profiles.Remove(resolvedCurrent);
            catalog.Profiles.Add(cleanName, profile);
            if (string.Equals(
                catalog.ActiveProfile,
                resolvedCurrent,
                StringComparison.OrdinalIgnoreCase))
            {
                catalog.ActiveProfile = cleanName;
            }

            SaveCatalog(catalog);
            return cleanName;
        }
    }

    public LayoutProfile Delete(string name)
    {
        lock (_sync)
        {
            var catalog = EnsureCatalog();
            if (catalog.Profiles.Count == 1)
            {
                throw new InvalidOperationException("O último perfil não pode ser excluído.");
            }

            var resolvedName = ResolveExistingName(catalog, name);
            catalog.Profiles.Remove(resolvedName);
            if (string.Equals(
                catalog.ActiveProfile,
                resolvedName,
                StringComparison.OrdinalIgnoreCase))
            {
                catalog.ActiveProfile = catalog.Profiles.Keys
                    .OrderBy(item => item, StringComparer.CurrentCultureIgnoreCase)
                    .First();
            }

            SaveCatalog(catalog);
            return catalog.Profiles[catalog.ActiveProfile];
        }
    }

    public void Export(string name, string destinationPath)
    {
        lock (_sync)
        {
            var catalog = EnsureCatalog();
            var resolvedName = ResolveExistingName(catalog, name);
            var export = new LayoutProfileExport(
                1,
                resolvedName,
                catalog.Profiles[resolvedName]);
            WriteAtomic(destinationPath, JsonSerializer.Serialize(export, JsonOptions));
        }
    }

    public string Import(string sourcePath)
    {
        LayoutProfileExport export;
        try
        {
            export = JsonSerializer.Deserialize<LayoutProfileExport>(
                File.ReadAllText(sourcePath),
                JsonOptions) ?? throw new InvalidDataException("Arquivo de perfil inválido.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Arquivo de perfil inválido.", exception);
        }

        if (export.FormatVersion != 1 ||
            export.Profile.SchemaVersion != LayoutProfile.CurrentSchemaVersion)
        {
            throw new InvalidDataException("Versão de perfil incompatível.");
        }

        lock (_sync)
        {
            var catalog = EnsureCatalog();
            var baseName = NormalizeName(export.Name);
            var uniqueName = MakeUniqueName(catalog, baseName);
            catalog.Profiles.Add(uniqueName, Sanitize(export.Profile));
            catalog.ActiveProfile = uniqueName;
            SaveCatalog(catalog);
            return uniqueName;
        }
    }

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
            if (legacy is { SchemaVersion: LayoutProfile.CurrentSchemaVersion })
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

    private static LayoutCatalog CreateDefaultCatalog() => new(
        CatalogSchemaVersion,
        DefaultProfileName,
        new Dictionary<string, LayoutProfile>(StringComparer.OrdinalIgnoreCase)
        {
            [DefaultProfileName] = LayoutProfile.Default,
        });

    private static string ResolveExistingName(LayoutCatalog catalog, string name) =>
        catalog.Profiles.Keys.FirstOrDefault(item =>
            string.Equals(item, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Perfil '{name}' não encontrado.");

    private static string ValidateNewName(LayoutCatalog catalog, string name)
    {
        var cleanName = NormalizeName(name);
        if (catalog.Profiles.ContainsKey(cleanName))
        {
            throw new InvalidOperationException($"Já existe um perfil chamado '{cleanName}'.");
        }

        return cleanName;
    }

    private static string NormalizeName(string name)
    {
        var cleanName = name.Trim();
        if (cleanName.Length is < 1 or > 40)
        {
            throw new InvalidOperationException("O nome deve ter entre 1 e 40 caracteres.");
        }

        if (cleanName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidOperationException("O nome contém caracteres inválidos.");
        }

        return cleanName;
    }

    private static string MakeUniqueName(LayoutCatalog catalog, string baseName)
    {
        if (!catalog.Profiles.ContainsKey(baseName))
        {
            return baseName;
        }

        for (var suffix = 2; suffix < 1000; suffix++)
        {
            var candidate = $"{baseName} ({suffix})";
            if (!catalog.Profiles.ContainsKey(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Não foi possível gerar um nome único.");
    }

    private static LayoutProfile Sanitize(LayoutProfile profile)
    {
        var liveStandings = SanitizePlacement(profile.LiveStandings);
        if (profile.SchemaVersion < 5 &&
            Math.Abs(liveStandings.Width - 0.25) < 0.001)
        {
            liveStandings = liveStandings with
            {
                X = Math.Abs(liveStandings.X - 0.72) < 0.001
                    ? 0.81
                    : liveStandings.X,
                Width = 0.16,
                Opacity = Math.Max(0.96, liveStandings.Opacity),
            };
        }

        var fuelStrategy = SanitizePlacement(profile.FuelStrategy);
        if (profile.SchemaVersion < 6 &&
            Math.Abs(fuelStrategy.Width - 0.22) < 0.001 &&
            Math.Abs(fuelStrategy.Height - 0.16) < 0.001)
        {
            fuelStrategy = fuelStrategy with
            {
                Width = 0.30,
                Height = 0.25,
                Opacity = Math.Max(0.96, fuelStrategy.Opacity),
            };
        }

        var relative = SanitizePlacement(profile.Relative);
        if (profile.SchemaVersion < 7 &&
            Math.Abs(relative.Width - 0.28) < 0.001 &&
            Math.Abs(relative.Height - 0.28) < 0.001)
        {
            relative = relative with
            {
                X = 0.64,
                Y = 0.05,
                Width = 0.16,
                Height = 0.40,
                Opacity = Math.Max(0.96, relative.Opacity),
            };
        }

        var sessionFlags = SanitizePlacement(profile.SessionFlags);
        if (profile.SchemaVersion < 8 &&
            Math.Abs(sessionFlags.Width - 0.28) < 0.001 &&
            Math.Abs(sessionFlags.Height - 0.12) < 0.001)
        {
            sessionFlags = sessionFlags with
            {
                X = 0.33,
                Width = 0.30,
                Height = 0.18,
                Opacity = Math.Max(0.96, sessionFlags.Opacity),
            };
        }

        return profile with
        {
            SchemaVersion = LayoutProfile.CurrentSchemaVersion,
            Diagnostic = SanitizePlacement(profile.Diagnostic),
            Inputs = SanitizePlacement(profile.Inputs),
            LiveStandings = liveStandings,
            Relative = relative,
            SessionFlags = sessionFlags,
            FuelStrategy = fuelStrategy,
        };
    }

    private static WidgetPlacement SanitizePlacement(WidgetPlacement item) => item with
    {
        X = Math.Clamp(item.X, 0, 0.95),
        Y = Math.Clamp(item.Y, 0, 0.95),
        Width = Math.Clamp(item.Width, 0.08, 1),
        Height = Math.Clamp(item.Height, 0.08, 1),
        Scale = Math.Clamp(item.Scale, 0.5, 2),
        Opacity = Math.Clamp(item.Opacity, 0.2, 1),
    };

    private sealed class LayoutCatalog
    {
        public LayoutCatalog(
            int schemaVersion,
            string activeProfile,
            Dictionary<string, LayoutProfile> profiles)
        {
            SchemaVersion = schemaVersion;
            ActiveProfile = activeProfile;
            Profiles = profiles;
        }

        public int SchemaVersion { get; }
        public string ActiveProfile { get; set; }
        public Dictionary<string, LayoutProfile> Profiles { get; }
    }

    private sealed record LayoutProfileExport(
        int FormatVersion,
        string Name,
        LayoutProfile Profile);
}
