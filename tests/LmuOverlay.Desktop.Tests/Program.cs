using LmuOverlay.Desktop;

var root = Path.Combine(Path.GetTempPath(), "lmu-overlay-tests", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);

try
{
    var path = Path.Combine(root, "layout.json");
    var store = new LayoutStore(path);
    Assert(store.Load() == LayoutProfile.Default, "Missing profile must load defaults.");

    var requested = new LayoutProfile(
        LayoutProfile.CurrentSchemaVersion,
        new WidgetPlacement(-2, 4, 0.01, 5, 8, -1, true),
        LayoutProfile.Default.Inputs,
        LayoutProfile.Default.LiveStandings);
    store.Save(requested);
    var loaded = store.Load();

    Assert(loaded.Diagnostic.X == 0, "X must be clamped.");
    Assert(loaded.Diagnostic.Y == 0.95, "Y must be clamped.");
    Assert(loaded.Diagnostic.Width == 0.08, "Width must respect its minimum.");
    Assert(loaded.Diagnostic.Height == 1, "Height must be clamped.");
    Assert(loaded.Diagnostic.Scale == 2, "Scale must be clamped.");
    Assert(loaded.Diagnostic.Opacity == 0.2, "Opacity must be clamped.");

    File.WriteAllText(path, "{broken");
    Assert(store.Load() == LayoutProfile.Default, "Corrupt profiles must be recoverable.");

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
