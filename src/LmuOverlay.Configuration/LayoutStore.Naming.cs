using System.IO;
using System.Text.Json;

namespace LmuOverlay.Configuration;

public sealed partial class LayoutStore
{
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
}
