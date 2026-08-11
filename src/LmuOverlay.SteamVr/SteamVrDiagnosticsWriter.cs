using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using LmuOverlay.Core;

namespace LmuOverlay.SteamVr;

public static class SteamVrDiagnosticsWriter
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static bool TryWrite(
        string path,
        TelemetryRuntimeHealth telemetry,
        PresentationHostHealth presentation,
        SteamVrProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var report = new
        {
            FormatVersion = 1,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Host = "SteamVR",
            Application = new
            {
                Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
                Framework = RuntimeInformation.FrameworkDescription,
                OperatingSystem = RuntimeInformation.OSDescription,
                Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            },
            Telemetry = new
            {
                telemetry.SuccessfulReads,
                telemetry.FailedReads,
                telemetry.Reconnects,
                telemetry.EventWakeups,
                telemetry.EventTimeouts,
                telemetry.PublishedSnapshots,
                telemetry.LastReadMilliseconds,
                telemetry.AverageReadMilliseconds,
                telemetry.MaximumReadMilliseconds,
                telemetry.P99ReadMilliseconds,
                telemetry.StaleAgeMilliseconds,
                telemetry.LastSuccessfulRead,
                telemetry.LastError,
            },
            Presentation = presentation,
            Layout = new
            {
                profile.SchemaVersion,
                SurfaceCount = 8,
            },
            Privacy = "Driver, player, track and raw telemetry identities are intentionally omitted.",
        };
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(report, Options));
            File.Move(temporary, path, true);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
