using System.IO;
using System.Text.Json;

namespace LmuOverlay.Configuration;

public sealed partial class LayoutStore
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





}
