using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace LmuOverlay.Core;

public enum GameCompatibilityState
{
    GameNotFound,
    HeaderNotFound,
    KnownLayout,
    UnknownLayout,
}

public sealed record HeaderFingerprint(string FileName, string Sha256, long SizeBytes);

public sealed record GameCompatibilityReport(
    GameCompatibilityState State,
    string InstalledBuildId,
    string TargetBuildId,
    bool UpdatePending,
    string InstallationPath,
    IReadOnlyList<HeaderFingerprint> Headers,
    string Detail)
{
    public string CompatibilityGeneration =>
        $"{(InstalledBuildId.Length > 0 ? InstalledBuildId : "unknown-build")}:" +
        (Headers.FirstOrDefault(item =>
            item.FileName.Equals("SharedMemoryInterface.hpp", StringComparison.OrdinalIgnoreCase))
            ?.Sha256 ?? "unknown-layout");
}

public static partial class GameCompatibilityProbe
{
    public const string KnownSharedMemoryHeaderSha256 =
        "194ff1ab39030bc811540931c8b9817258727252c9a4b35fa4734bbaa16d4ddc";

    private static readonly string[] HeaderNames =
    [
        "SharedMemoryInterface.hpp",
        "InternalsPlugin.hpp",
        "PluginObjects.hpp",
    ];

    public static GameCompatibilityReport Detect(string? steamAppsPath = null)
    {
        var apps = ResolveSteamApps(steamAppsPath);
        var manifest = apps is null ? null : Path.Combine(apps, "appmanifest_2399420.acf");
        var values = ReadManifest(manifest);
        var installation = values.TryGetValue("installdir", out var installDirectory) && apps is not null
            ? Path.Combine(apps, "common", installDirectory)
            : string.Empty;
        var installedBuild = values.GetValueOrDefault("buildid", string.Empty);
        var targetBuild = values.GetValueOrDefault("TargetBuildID", string.Empty);
        var pending = targetBuild.Length > 0 &&
            !string.Equals(installedBuild, targetBuild, StringComparison.Ordinal);

        if (installation.Length == 0 || !Directory.Exists(installation))
        {
            return new(GameCompatibilityState.GameNotFound, installedBuild, targetBuild, pending,
                installation, [], "Le Mans Ultimate installation was not found.");
        }

        var support = Path.Combine(installation, "Support", "SharedMemoryInterface");
        var headers = HeaderNames.Select(name => Fingerprint(Path.Combine(support, name)))
            .Where(item => item is not null)
            .Cast<HeaderFingerprint>()
            .ToArray();
        var shared = headers.FirstOrDefault(item =>
            item.FileName.Equals("SharedMemoryInterface.hpp", StringComparison.OrdinalIgnoreCase));
        if (shared is null)
        {
            return new(GameCompatibilityState.HeaderNotFound, installedBuild, targetBuild, pending,
                installation, headers, "The official shared-memory header was not found.");
        }

        var known = shared.Sha256.Equals(KnownSharedMemoryHeaderSha256,
            StringComparison.OrdinalIgnoreCase);
        return new(
            known ? GameCompatibilityState.KnownLayout : GameCompatibilityState.UnknownLayout,
            installedBuild,
            targetBuild,
            pending,
            installation,
            headers,
            known
                ? "The installed shared-memory header matches layout v1."
                : "Unknown shared-memory header. Telemetry must be validated before compatibility is claimed.");
    }

    private static HeaderFingerprint? Fingerprint(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var stream = File.OpenRead(path);
            return new(Path.GetFileName(path), Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(),
                stream.Length);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static Dictionary<string, string> ParseManifest(string text)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in ManifestPair().Matches(text))
        {
            values[match.Groups[1].Value] = match.Groups[2].Value;
        }
        return values;
    }

    private static Dictionary<string, string> ReadManifest(string? path)
    {
        try
        {
            return path is not null && File.Exists(path)
                ? ParseManifest(File.ReadAllText(path))
                : new(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string? ResolveSteamApps(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath)) return Path.GetFullPath(explicitPath);
        var candidates = new List<string>();
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                if (key?.GetValue("SteamPath") is string steam)
                    candidates.Add(Path.Combine(steam, "steamapps"));
            }
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
        }
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Steam", "steamapps"));
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Steam", "steamapps"));
        foreach (var primary in candidates.ToArray())
        {
            var folders = Path.Combine(primary, "libraryfolders.vdf");
            try
            {
                if (!File.Exists(folders)) continue;
                foreach (Match match in LibraryPath().Matches(File.ReadAllText(folders)))
                {
                    var library = match.Groups[1].Value.Replace("\\\\", "\\", StringComparison.Ordinal);
                    candidates.Add(Path.Combine(library, "steamapps"));
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
        return candidates.FirstOrDefault(path => File.Exists(Path.Combine(path, "appmanifest_2399420.acf")));
    }

    [GeneratedRegex("\\\"([^\\\"]+)\\\"\\s+\\\"([^\\\"]*)\\\"", RegexOptions.CultureInvariant)]
    private static partial Regex ManifestPair();

    [GeneratedRegex("\\\"path\\\"\\s+\\\"([^\\\"]+)\\\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LibraryPath();
}

public sealed record VrRuntimeReport(
    bool SteamVrInstalled,
    bool SteamVrRunning,
    string OpenVrLibraryPath,
    string ActiveOpenXrRuntime,
    bool SteamVrIsActiveOpenXrRuntime,
    bool OpenXrExperimentalAvailable,
    string Detail);

public static class VrRuntimeProbe
{
    public static VrRuntimeReport Detect()
    {
        var openVr = FindOpenVrLibrary();
        var activeOpenXr = ReadActiveOpenXrRuntime();
        var steamVrOpenXr = activeOpenXr.Contains("steamvr", StringComparison.OrdinalIgnoreCase) ||
            activeOpenXr.Contains("steamxr", StringComparison.OrdinalIgnoreCase);
        var running = new[] { "vrserver", "vrcompositor", "vrmonitor" }
            .Any(name => Process.GetProcessesByName(name).Length > 0);
        var detail = openVr.Length == 0
            ? "SteamVR/OpenVR was not found."
            : steamVrOpenXr
                ? "SteamVR is the active OpenXR runtime."
                : activeOpenXr.Length == 0
                    ? "No active OpenXR runtime was registered."
                    : "A non-SteamVR OpenXR runtime is active; IVROverlay cannot be assumed visible.";
        return new(openVr.Length > 0, running, openVr, activeOpenXr, steamVrOpenXr,
            activeOpenXr.Length > 0, detail);
    }

    private static string ReadActiveOpenXrRuntime()
    {
        if (!OperatingSystem.IsWindows()) return string.Empty;
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            try
            {
                using var key = hive.OpenSubKey(@"SOFTWARE\Khronos\OpenXR\1");
                if (key?.GetValue("ActiveRuntime") is string value && value.Length > 0) return value;
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
            {
            }
        }
        return string.Empty;
    }

    private static string FindOpenVrLibrary()
    {
        if (!OperatingSystem.IsWindows()) return string.Empty;
        var architecture = Environment.Is64BitProcess ? "win64" : "win32";
        var candidates = new List<string>();
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            if (key?.GetValue("SteamPath") is string steam)
                candidates.Add(Path.Combine(steam, "steamapps", "common", "SteamVR", "bin",
                    architecture, "openvr_api.dll"));
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
        }
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "openvr_api.dll"));
        try
        {
            var paths = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "openvr", "openvrpaths.vrpath");
            if (File.Exists(paths))
            {
                using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(paths));
                if (document.RootElement.TryGetProperty("runtime", out var runtimes))
                {
                    foreach (var runtime in runtimes.EnumerateArray())
                    {
                        var root = runtime.GetString();
                        if (!string.IsNullOrWhiteSpace(root))
                            candidates.Add(Path.Combine(root, "bin", architecture, "openvr_api.dll"));
                    }
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
        }
        return candidates.FirstOrDefault(File.Exists) ?? string.Empty;
    }
}
