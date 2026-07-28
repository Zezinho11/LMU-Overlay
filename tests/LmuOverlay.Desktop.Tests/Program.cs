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
    store.Save(requested);
    var loaded = store.Load();

    Assert(loaded.Diagnostic.X == 0, "X must be clamped.");
    Assert(loaded.Diagnostic.Y == 0.95, "Y must be clamped.");
    Assert(loaded.Diagnostic.Width == 0.08, "Width must respect its minimum.");
    Assert(loaded.Diagnostic.Height == 1, "Height must be clamped.");
    Assert(loaded.Diagnostic.Scale == 2, "Scale must be clamped.");
    Assert(loaded.Diagnostic.Opacity == 0.2, "Opacity must be clamped.");

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
