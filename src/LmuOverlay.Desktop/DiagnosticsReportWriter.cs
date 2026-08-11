using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using LmuOverlay.Core;
using LmuOverlay.Domain;

namespace LmuOverlay.Desktop;

public sealed record DesktopPresentationHealth(
    PresentationHostHealth Dashboard,
    PresentationHostHealth Inputs,
    PresentationHostHealth Timing)
{
    private static readonly PresentationHostHealth Unavailable =
        new(false, 0, null, string.Empty);
    public static DesktopPresentationHealth Empty { get; } =
        new(Unavailable, Unavailable, Unavailable);
}

public static class DiagnosticsReportWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    public static void Write(
        string path,
        LmuTelemetrySnapshot snapshot,
        TelemetryRuntimeHealth health,
        DesktopPresentationHealth presentation,
        LayoutProfile profile,
        string activeProfile)
    {
        var process = Process.GetCurrentProcess();
        var budget = RuntimePerformanceBudget.Evaluate(
            health,
            process.WorkingSet64);
        var report = new
        {
            FormatVersion = 1,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Application = new
            {
                Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
                Framework = RuntimeInformation.FrameworkDescription,
                OperatingSystem = RuntimeInformation.OSDescription,
                Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            },
            Telemetry = new
            {
                State = snapshot.State.ToString(),
                snapshot.GameVersion,
                snapshot.ActiveVehicles,
                snapshot.ScoredVehicles,
                SessionKind = snapshot.Session?.Kind.ToString(),
                AgeMilliseconds = Math.Max(
                    0,
                    (DateTimeOffset.UtcNow - snapshot.CapturedAt).TotalMilliseconds),
            },
            Runtime = new
            {
                health.SuccessfulReads,
                health.FailedReads,
                health.Reconnects,
                health.EventWakeups,
                health.EventTimeouts,
                health.DuplicateSnapshots,
                health.PublishedSnapshots,
                health.LastReadMilliseconds,
                health.AverageReadMilliseconds,
                health.MaximumReadMilliseconds,
                health.P99ReadMilliseconds,
                health.StaleAgeMilliseconds,
                health.LastSuccessfulRead,
                health.LastError,
                WorkingSetMegabytes = process.WorkingSet64 / 1024d / 1024d,
                PrivateMemoryMegabytes = process.PrivateMemorySize64 / 1024d / 1024d,
            },
            PerformanceBudget = new
            {
                budget.WithinBudget,
                budget.AverageReadWithinBudget,
                budget.MaximumReadWithinBudget,
                budget.WorkingSetWithinBudget,
                budget.AverageReadLimitMilliseconds,
                budget.MaximumReadLimitMilliseconds,
                budget.WorkingSetLimitMegabytes,
            },
            Presentation = new
            {
                Dashboard = presentation.Dashboard,
                Inputs = presentation.Inputs,
                Timing = presentation.Timing,
            },
            Layout = new
            {
                ActiveProfile = activeProfile,
                profile.SchemaVersion,
                profile.Settings.Theme,
                profile.Settings.RefreshRateHz,
                profile.Settings.GridSnapPixels,
                WidgetCount = 7,
            },
            Privacy = "Driver, player, track and raw telemetry identities are intentionally omitted.",
        };

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(report, Options));
        File.Move(temporary, path, true);
    }
}

public static class CrashLogWriter
{
    public static void TryWrite(Exception exception)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LMU Overlay",
                "logs");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(
                directory,
                $"crash-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.log");
            File.WriteAllText(
                path,
                $"{DateTimeOffset.UtcNow:O}{Environment.NewLine}" +
                $"{exception.GetType().FullName}: {exception.Message}{Environment.NewLine}" +
                exception.StackTrace);
        }
        catch (Exception writeException) when (
            writeException is IOException or UnauthorizedAccessException)
        {
        }
    }
}
