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
        var diagnostic = SanitizePlacement(profile.Diagnostic);
        if (profile.SchemaVersion < 10 &&
            diagnostic.Width <= 0.23 && diagnostic.Height <= 0.20)
        {
            diagnostic = diagnostic with
            {
                Width = 0.38,
                Height = 0.40,
                Opacity = Math.Max(0.96, diagnostic.Opacity),
            };
        }

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

        if (profile.SchemaVersion < 17)
        {
            liveStandings = ExpandTimingPanel(liveStandings);
            relative = ExpandTimingPanel(relative);
            if (Overlaps(liveStandings, relative))
            {
                relative = relative with
                {
                    X = Math.Max(0, liveStandings.X - relative.Width - 0.01),
                };
            }
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

        var inputs = SanitizePlacement(profile.Inputs);
        if (profile.SchemaVersion < 15 &&
            Math.Abs(inputs.X - 0.025) < 0.001 &&
            Math.Abs(inputs.Y - 0.47) < 0.001)
        {
            inputs = inputs with { Y = 0.66 };
        }

        var settings = SanitizeSettings(profile.Settings);
        if (profile.SchemaVersion < 16 && settings.RefreshRateHz <= 60)
        {
            settings = settings with { RefreshRateHz = 120 };
        }

        return profile with
        {
            SchemaVersion = LayoutProfile.CurrentSchemaVersion,
            Diagnostic = diagnostic,
            Inputs = inputs,
            LiveStandings = liveStandings,
            Relative = relative,
            SessionFlags = sessionFlags,
            FuelStrategy = fuelStrategy,
            RaceControl = SanitizePlacement(profile.RaceControl),
            Settings = settings,
        };
    }

    private static WidgetPlacement ExpandTimingPanel(WidgetPlacement placement)
    {
        if (placement.Width > 0.20 || placement.Height < 0.34)
        {
            return placement;
        }

        const double expandedWidth = 0.28;
        var x = placement.X >= 0.5
            ? placement.X + placement.Width - expandedWidth
            : placement.X;
        return placement with
        {
            X = Math.Clamp(x, 0, 1 - expandedWidth),
            Width = expandedWidth,
            Height = Math.Max(0.40, placement.Height),
        };
    }

    private static bool Overlaps(WidgetPlacement first, WidgetPlacement second) =>
        first.X < second.X + second.Width &&
        first.X + first.Width > second.X &&
        first.Y < second.Y + second.Height &&
        first.Y + first.Height > second.Y;

    private static OverlayProfileSettings SanitizeSettings(
        OverlayProfileSettings? settings)
    {
        settings ??= new();
        var theme = settings.Theme is "RedFox" or "HighContrast" or "ColorVisionSafe" or "Black" or "Custom"
            ? settings.Theme
            : "RedFox";
        var density = Enum.TryParse<OverlayDensity>(
            settings.VisualDensity,
            true,
            out var parsedDensity)
                ? parsedDensity.ToString()
                : OverlayDensity.Auto.ToString();
        return settings with
        {
            Language = LmuOverlay.Widgets.OverlayText.Normalize(settings.Language),
            Theme = theme,
            CustomAccentColor = OverlayVisualSystem.NormalizeHexColor(
                settings.CustomAccentColor,
                "#42D3A6"),
            CustomBackgroundColor = OverlayVisualSystem.NormalizeHexColor(
                settings.CustomBackgroundColor,
                "#0A0F1A"),
            CustomCardColor = OverlayVisualSystem.NormalizeHexColor(
                settings.CustomCardColor,
                "#121924"),
            CustomPrimaryTextColor = OverlayVisualSystem.NormalizeHexColor(
                settings.CustomPrimaryTextColor,
                "#FFFFFF"),
            CustomSecondaryTextColor = OverlayVisualSystem.NormalizeHexColor(
                settings.CustomSecondaryTextColor,
                "#CAD3DC"),
            CustomInformationColor = OverlayVisualSystem.NormalizeHexColor(
                settings.CustomInformationColor,
                "#12D9E5"),
            CustomAttentionColor = OverlayVisualSystem.NormalizeHexColor(
                settings.CustomAttentionColor,
                "#FFBE40"),
            CustomCriticalColor = OverlayVisualSystem.NormalizeHexColor(
                settings.CustomCriticalColor,
                "#FF464B"),
            CustomPositiveColor = OverlayVisualSystem.NormalizeHexColor(
                settings.CustomPositiveColor,
                "#42D3A6"),
            DashboardTitle = SanitizeDashboardTitle(settings.DashboardTitle),
            DashboardModuleOrder = LmuOverlay.Widgets.DashboardModuleLayout.Normalize(
                settings.DashboardModuleOrder),
            DashboardTextScale = Math.Clamp(
                settings.DashboardTextScale <= 0 ? 1 : settings.DashboardTextScale,
                0.8,
                1.25),
            TimingTextScale = Math.Clamp(
                settings.TimingTextScale <= 0 ? 1 : settings.TimingTextScale,
                0.8,
                1.25),
            InputsTextScale = Math.Clamp(
                settings.InputsTextScale <= 0 ? 1 : settings.InputsTextScale,
                0.8,
                1.25),
            LiveStandingsMaximumRows = Math.Clamp(
                settings.LiveStandingsMaximumRows <= 0 ? 12 : settings.LiveStandingsMaximumRows,
                6,
                12),
            RelativeCarsEachSide = Math.Clamp(
                settings.RelativeCarsEachSide <= 0 ? 4 : settings.RelativeCarsEachSide,
                2,
                5),
            VisualDensity = density,
            RefreshRateHz = Math.Clamp(settings.RefreshRateHz < 30 ? 120 : settings.RefreshRateHz, 30, 144),
            GridSnapPixels = Math.Clamp(settings.GridSnapPixels, 0, 50),
            FuelReserveLaps = Math.Clamp(settings.FuelReserveLaps, 0, 5),
            EnergyReservePercent = Math.Clamp(settings.EnergyReservePercent, 0, 25),
            ManualRemainingLaps = Math.Clamp(settings.ManualRemainingLaps, 0, 1000),
            ManualRemainingMinutes = Math.Clamp(settings.ManualRemainingMinutes, 0, 1440),
            ManualLapTimeSeconds = Math.Clamp(settings.ManualLapTimeSeconds, 0, 3600),
            ManualFuelPerLapLiters = Math.Clamp(settings.ManualFuelPerLapLiters, 0, 100),
            ManualFuelCapacityLiters = Math.Clamp(settings.ManualFuelCapacityLiters, 0, 1000),
            MaximumStintLaps = Math.Clamp(settings.MaximumStintLaps, 0, 1000),
            EstimatedPitLossSeconds = Math.Clamp(
                settings.EstimatedPitLossSeconds,
                0,
                600),
            AvailableTireSets = Math.Clamp(settings.AvailableTireSets, 0, 100),
            TireWearLimitPercent = Math.Clamp(settings.TireWearLimitPercent, 20, 95),
            EstimatedTireChangeSeconds = Math.Clamp(
                settings.EstimatedTireChangeSeconds,
                0,
                180),
            BackgroundOpacity = Math.Clamp(
                settings.BackgroundOpacity <= 0 ? 0.94 : settings.BackgroundOpacity,
                0.35,
                1),
            PedalHistorySeconds = Math.Clamp(
                settings.PedalHistorySeconds <= 0 ? 5 : settings.PedalHistorySeconds,
                3,
                10),
        };
    }

    private static string SanitizeDashboardTitle(string? value)
    {
        var title = string.IsNullOrWhiteSpace(value) ? "REDFOX RACING" : value.Trim();
        var printable = new string(title.Where(character => !char.IsControl(character)).ToArray());
        return printable.Length > 24 ? printable[..24] : printable;
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
