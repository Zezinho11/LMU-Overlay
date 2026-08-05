using LmuOverlay.Desktop;
using System.Text.Json;

var root = Path.Combine(Path.GetTempPath(), "lmu-overlay-tests", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);

try
{
    var path = Path.Combine(root, "layout.json");
    var store = new LayoutStore(path);
    Assert(store.Load() == LayoutProfile.Default, "Missing profile must load defaults.");
    Assert(store.ActiveProfileName == LayoutStore.DefaultProfileName, "Default profile must be active.");
    Assert(store.ProfileNames.Count == 1, "A fresh store must contain one profile.");

    var requested = new LayoutProfile(
        LayoutProfile.CurrentSchemaVersion,
        new WidgetPlacement(-2, 4, 0.01, 5, 8, -1, true),
        LayoutProfile.Default.Inputs,
        LayoutProfile.Default.LiveStandings,
        LayoutProfile.Default.Relative,
        LayoutProfile.Default.SessionFlags,
        LayoutProfile.Default.FuelStrategy);
    requested = requested with
    {
        RaceControl = new WidgetPlacement(2, -1, 0.01, 4, 5, 0.1, true),
        Settings = new OverlayProfileSettings
        {
            Theme = "Unsupported",
            RefreshRateHz = 100,
            GridSnapPixels = 80,
            FuelReserveLaps = 20,
            EnergyReservePercent = 50,
        },
    };
    store.Save(requested);
    var loaded = store.Load();

    Assert(loaded.Diagnostic.X == 0, "X must be clamped.");
    Assert(loaded.Diagnostic.Y == 0.95, "Y must be clamped.");
    Assert(loaded.Diagnostic.Width == 0.08, "Width must respect its minimum.");
    Assert(loaded.Diagnostic.Height == 1, "Height must be clamped.");
    Assert(loaded.Diagnostic.Scale == 2, "Scale must be clamped.");
    Assert(loaded.Diagnostic.Opacity == 0.2, "Opacity must be clamped.");
    Assert(loaded.RaceControl.X == 0.95 && loaded.RaceControl.Y == 0,
        "Race Control placement must be sanitized.");
    Assert(loaded.RaceControl.Scale == 2,
        "Race Control scale must be clamped.");
    Assert(loaded.Settings.Theme == "RedFox", "Unknown themes must fail to RedFox.");
    Assert(loaded.Settings.RefreshRateHz == 100, "Valid high refresh rate must be preserved.");
    Assert(loaded.Settings.GridSnapPixels == 50, "Grid snapping must be clamped.");
    Assert(loaded.Settings.FuelReserveLaps == 5, "Fuel reserve must be clamped.");

    store.Create("Endurance", loaded);
    Assert(store.ActiveProfileName == "Endurance", "New profiles must become active.");
    Assert(store.ProfileNames.Count == 2, "Created profiles must be listed.");
    store.Save(loaded with
    {
        Inputs = loaded.Inputs with { Visible = false },
    });
    Assert(!store.Load().Inputs.Visible, "Changes must be saved to the active profile.");

    store.Switch(LayoutStore.DefaultProfileName);
    Assert(store.Load().Inputs.Visible, "Switching profiles must restore independent settings.");
    Assert(store.Rename(LayoutStore.DefaultProfileName, "Corrida") == "Corrida",
        "Profiles must be renameable.");
    Assert(store.ActiveProfileName == "Corrida", "Renaming must preserve the active profile.");

    var exportPath = Path.Combine(root, "corrida.lmu-layout.json");
    store.Export("Corrida", exportPath);
    var importedName = store.Import(exportPath);
    Assert(importedName == "Corrida (2)", "Import must resolve duplicate names.");
    Assert(store.ActiveProfileName == importedName, "Imported profiles must become active.");
    Assert(store.Load() == store.Switch("Corrida"), "Imported settings must match their source.");

    store.Delete("Endurance");
    Assert(!store.ProfileNames.Contains("Endurance"), "Deleted profiles must be removed.");
    AssertThrows<InvalidOperationException>(
        () =>
        {
            store.Delete("Corrida");
            store.Delete("Corrida (2)");
        },
        "The last profile must not be removable.");

    var legacyPath = Path.Combine(root, "legacy-layout.json");
    File.WriteAllText(legacyPath, JsonSerializer.Serialize(LayoutProfile.Default));
    var legacyStore = new LayoutStore(legacyPath);
    Assert(legacyStore.Load() == LayoutProfile.Default, "Legacy single profiles must migrate.");
    Assert(legacyStore.ProfileNames.Count == 1, "Migration must create one catalog profile.");
    legacyStore.Create("Migrated copy", legacyStore.Load());
    Assert(legacyStore.ProfileNames.Count == 2, "Migrated catalogs must remain writable.");

    foreach (var (width, height) in new[]
    {
        (1280d, 720d),
        (1920d, 1080d),
        (2560d, 1080d),
        (3440d, 1440d),
        (3840d, 2160d),
    })
    {
        foreach (var (name, placement) in new[]
        {
            ("DiagnosticWidget", LayoutProfile.Default.Diagnostic),
            ("InputsWidget", LayoutProfile.Default.Inputs),
            ("LiveStandingsWidget", LayoutProfile.Default.LiveStandings),
            ("RelativeWidget", LayoutProfile.Default.Relative),
            ("SessionFlagsWidget", LayoutProfile.Default.SessionFlags),
            ("FuelStrategyWidget", LayoutProfile.Default.FuelStrategy),
            ("RaceControlWidget", LayoutProfile.Default.RaceControl),
        })
        {
            var spec = ResponsiveWidgetLayout.For(name);
            var bounds = ResponsiveWidgetLayout.Calculate(width, height, placement, spec);
            Assert(bounds.Width > 0 && bounds.Height > 0,
                $"{name} must remain visible at {width}x{height}.");
            Assert(bounds.X >= 0 && bounds.Y >= 0 &&
                   bounds.X + bounds.Width <= width + 0.001 &&
                   bounds.Y + bounds.Height <= height + 0.001,
                $"{name} must remain inside {width}x{height}.");
            Assert(Math.Abs(bounds.Width / bounds.Height - spec.AspectRatio) < 0.001,
                $"{name} must preserve its aspect ratio at {width}x{height}.");
        }

        var fuelBounds = ResponsiveWidgetLayout.Calculate(
            width,
            height,
            LayoutProfile.Default.FuelStrategy,
            ResponsiveWidgetLayout.For("FuelStrategyWidget"));
        var inputBounds = ResponsiveWidgetLayout.Calculate(
            width,
            height,
            LayoutProfile.Default.Inputs,
            ResponsiveWidgetLayout.For("InputsWidget"));
        Assert(
            fuelBounds.Y + fuelBounds.Height <= inputBounds.Y ||
            inputBounds.Y + inputBounds.Height <= fuelBounds.Y,
            $"Fuel and inputs must not overlap at {width}x{height}.");
    }

    var narrowTowerPath = Path.Combine(root, "layout-v4.json");
    var versionFourProfile = LayoutProfile.Default with
    {
        SchemaVersion = 4,
        LiveStandings = LayoutProfile.Default.LiveStandings with
        {
            X = 0.72,
            Width = 0.25,
            Opacity = 0.92,
        },
    };
    File.WriteAllText(narrowTowerPath, JsonSerializer.Serialize(new
    {
        SchemaVersion = 1,
        ActiveProfile = "Legacy",
        Profiles = new Dictionary<string, LayoutProfile>
        {
            ["Legacy"] = versionFourProfile,
        },
    }));
    var narrowTowerStore = new LayoutStore(narrowTowerPath);
    var migratedTower = narrowTowerStore.Load().LiveStandings;
    Assert(migratedTower.Width == 0.16, "The old wide standings layout must migrate.");
    Assert(migratedTower.X == 0.81, "The narrow timing tower must remain right aligned.");

    var fuelPanelPath = Path.Combine(root, "layout-v5.json");
    var versionFiveProfile = LayoutProfile.Default with
    {
        SchemaVersion = 5,
        FuelStrategy = new WidgetPlacement(
            0.025, 0.38, 0.22, 0.16, 1, 0.92, true),
    };
    File.WriteAllText(fuelPanelPath, JsonSerializer.Serialize(new
    {
        SchemaVersion = 1,
        ActiveProfile = "Legacy Fuel",
        Profiles = new Dictionary<string, LayoutProfile>
        {
            ["Legacy Fuel"] = versionFiveProfile,
        },
    }));
    var fuelPanelStore = new LayoutStore(fuelPanelPath);
    var migratedFuelPanel = fuelPanelStore.Load().FuelStrategy;
    Assert(migratedFuelPanel.Width == 0.30,
        "The old fuel widget must migrate to the strategy-table width.");
    Assert(migratedFuelPanel.Height == 0.25,
        "The old fuel widget must migrate to the strategy-table height.");

    var relativeTowerPath = Path.Combine(root, "layout-v6.json");
    var versionSixProfile = LayoutProfile.Default with
    {
        SchemaVersion = 6,
        Relative = new WidgetPlacement(
            0.36, 0.58, 0.28, 0.28, 1, 0.92, true),
    };
    File.WriteAllText(relativeTowerPath, JsonSerializer.Serialize(new
    {
        SchemaVersion = 1,
        ActiveProfile = "Legacy Relative",
        Profiles = new Dictionary<string, LayoutProfile>
        {
            ["Legacy Relative"] = versionSixProfile,
        },
    }));
    var relativeTowerStore = new LayoutStore(relativeTowerPath);
    var migratedRelative = relativeTowerStore.Load().Relative;
    Assert(migratedRelative.Width == 0.16 && migratedRelative.Height == 0.40,
        "The old relative box must migrate to the timing-tower proportions.");
    Assert(migratedRelative.X == 0.64 && migratedRelative.Y == 0.05,
        "The relative tower must migrate beside live standings.");

    var sessionPanelPath = Path.Combine(root, "layout-v7.json");
    var versionSevenProfile = LayoutProfile.Default with
    {
        SchemaVersion = 7,
        SessionFlags = new WidgetPlacement(
            0.36, 0.05, 0.28, 0.12, 1, 0.92, true),
    };
    File.WriteAllText(sessionPanelPath, JsonSerializer.Serialize(new
    {
        SchemaVersion = 1,
        ActiveProfile = "Legacy Session",
        Profiles = new Dictionary<string, LayoutProfile>
        {
            ["Legacy Session"] = versionSevenProfile,
        },
    }));
    var sessionPanelStore = new LayoutStore(sessionPanelPath);
    var migratedSession = sessionPanelStore.Load().SessionFlags;
    Assert(migratedSession.Width == 0.30 && migratedSession.Height == 0.18,
        "The old session strip must migrate to the three-card panel.");
    Assert(migratedSession.X == 0.33,
        "The migrated session panel must stay centered.");

    var oldInputProfilePath = Path.Combine(root, "layout-v14.json");
    var oldInputProfile = LayoutProfile.Default with
    {
        SchemaVersion = 14,
        Inputs = LayoutProfile.Default.Inputs with { Y = 0.47 },
    };
    File.WriteAllText(oldInputProfilePath, JsonSerializer.Serialize(oldInputProfile));
    var migratedInputs = new LayoutStore(oldInputProfilePath).Load().Inputs;
    Assert(migratedInputs.Y == 0.66,
        "The old default input panel must migrate below fuel without overlap.");

    foreach (var theme in new[] { "RedFox", "Black", "HighContrast" })
    {
        var palette = OverlayVisualSystem.Resolve(theme);
        Assert(
            OverlayVisualSystem.ContrastRatio(
                palette.PrimaryText,
                palette.Background) >= 4.5,
            $"{theme} primary text must meet the visual contrast target.");
        Assert(
            OverlayVisualSystem.ContrastRatio(
                palette.SecondaryText,
                palette.Background) >= 3,
            $"{theme} secondary text must remain distinguishable.");
    }

    Assert(
        OverlayVisualSystem.ResolveDensity("Auto", 480, 800) == OverlayDensity.Compact,
        "Small automatic dashboards must use compact density.");
    Assert(
        OverlayVisualSystem.ResolveDensity("Expanded", 480, 800) == OverlayDensity.Expanded,
        "Explicit visual density must override automatic breakpoints.");
    foreach (var presetName in LayoutPresets.Names)
    {
        var preset = LayoutPresets.Create(presetName);
        Assert(preset.SchemaVersion == LayoutProfile.CurrentSchemaVersion,
            $"{presetName} must use the current profile schema.");
    }

    File.WriteAllText(path, "{broken");
    var corruptStore = new LayoutStore(path);
    Assert(corruptStore.Load() == LayoutProfile.Default, "Corrupt profiles must be recoverable.");

    Console.WriteLine("Desktop layout checks passed.");
    return 0;
}
finally
{
    Directory.Delete(root, true);
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertThrows<TException>(Action action, string message)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}
