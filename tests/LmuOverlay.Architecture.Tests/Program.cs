using System.Xml.Linq;

var root = FindRepositoryRoot();
Require(root is not null, "Repository root containing LmuOverlay.slnx was not found.");

var sourceRoot = Path.Combine(root!, "src");
var sourceFiles = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
    .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(segment =>
        segment is "obj" or "bin"))
    .ToArray();
var oversized = sourceFiles
    .Select(path => (Path: path, Lines: File.ReadLines(path).Count()))
    .Where(item => item.Lines > 450)
    .OrderByDescending(item => item.Lines)
    .ToArray();
Require(oversized.Length == 0,
    "Source files must stay below the 450-line architecture budget: " +
    string.Join(", ", oversized.Select(item => $"{Relative(item.Path)} ({item.Lines})")));

Require(LineCount("src/LmuOverlay.Desktop/App.xaml.cs") <= 100,
    "Desktop App.xaml.cs must remain a composition root.");
Require(LineCount("src/LmuOverlay.SteamVr/Program.cs") <= 30,
    "SteamVR Program.cs must remain a composition root.");

foreach (var facade in new[]
{
    "src/LmuOverlay.Configuration/LayoutStore.cs",
    "src/LmuOverlay.LmuSharedMemory/LmuSnapshotParser.cs",
    "src/LmuOverlay.Timing/SectorReferenceTracker.cs",
    "src/LmuOverlay.Widgets/EssentialWidgetStateFactory.cs",
    "src/LmuOverlay.Widgets/FuelStrategyTracker.cs",
    "src/LmuOverlay.SteamVr/VrWidgetTextureRenderer.cs",
})
{
    Require(LineCount(facade) <= 120, $"{facade} must remain a small facade/state holder.");
}

AssertProjectReferences("LmuOverlay.Domain");
AssertProjectReferences("LmuOverlay.Configuration");
AssertProjectReferences("LmuOverlay.Strategy", "LmuOverlay.Domain");
AssertProjectReferences("LmuOverlay.Timing", "LmuOverlay.Domain");
AssertProjectReferences("LmuOverlay.Application", "LmuOverlay.Domain", "LmuOverlay.Widgets");

AssertNoPlatformReferences("LmuOverlay.Domain");
AssertNoPlatformReferences("LmuOverlay.Configuration");
AssertNoPlatformReferences("LmuOverlay.Strategy");
AssertNoPlatformReferences("LmuOverlay.Timing");
AssertNoPlatformReferences("LmuOverlay.Widgets");

Console.WriteLine("Architecture boundary checks passed.");
return 0;

void AssertProjectReferences(string project, params string[] expected)
{
    var path = Path.Combine(sourceRoot, project, $"{project}.csproj");
    var actual = XDocument.Load(path)
        .Descendants("ProjectReference")
        .Select(element => Path.GetFileNameWithoutExtension(element.Attribute("Include")?.Value))
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();
    var wanted = expected.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    Require(actual.SequenceEqual(wanted, StringComparer.Ordinal),
        $"{project} references [{string.Join(", ", actual)}], expected [{string.Join(", ", wanted)}].");
}

void AssertNoPlatformReferences(string project)
{
    var folder = Path.Combine(sourceRoot, project);
    var forbidden = new[]
    {
        "System.Windows", "System.Windows.Forms", "Vortice.", "OpenVrNative",
        "LmuOverlay.Desktop", "LmuOverlay.DirectX", "LmuOverlay.SteamVr",
    };
    foreach (var path in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
    {
        var text = File.ReadAllText(path);
        foreach (var token in forbidden)
        {
            Require(!text.Contains(token, StringComparison.Ordinal),
                $"Stable module {project} contains platform dependency '{token}' in {Relative(path)}.");
        }
    }
}

int LineCount(string relativePath) =>
    File.ReadLines(Path.Combine(root!, relativePath.Replace('/', Path.DirectorySeparatorChar))).Count();

string Relative(string path) => Path.GetRelativePath(root!, path).Replace('\\', '/');

static string? FindRepositoryRoot()
{
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
         directory is not null;
         directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(directory.FullName, "LmuOverlay.slnx")))
            return directory.FullName;
    }
    return null;
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException($"Architecture check failed: {message}");
}
