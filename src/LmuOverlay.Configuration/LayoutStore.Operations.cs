using System.IO;
using System.Text.Json;

namespace LmuOverlay.Configuration;

public sealed partial class LayoutStore
{
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
            export.Profile.SchemaVersion is < 1 or > LayoutProfile.CurrentSchemaVersion)
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
}
