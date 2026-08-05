using System.Diagnostics;
using LmuOverlay.Contracts;
using LmuOverlay.Core;
using LmuOverlay.Domain;

var options = SoakOptions.Parse(args);
using var process = Process.GetCurrentProcess();
GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();

var source = new StableTelemetrySource();
var allocatedBefore = GC.GetTotalAllocatedBytes(true);
var cpuBefore = process.TotalProcessorTime;
var workingSetBefore = process.WorkingSet64;
var started = Stopwatch.StartNew();

TelemetryRuntimeHealth health;
await using (var runtime = new TelemetryRuntime(
    () => source,
    TimeSpan.FromSeconds(1d / options.PollRateHz),
    TimeSpan.FromMilliseconds(250)))
{
    runtime.Start();
    await Task.Delay(options.Duration);
    health = runtime.Health;
}

started.Stop();
process.Refresh();
var allocatedBytes = Math.Max(0, GC.GetTotalAllocatedBytes(true) - allocatedBefore);
var reads = Math.Max(1, health.SuccessfulReads);
var actualRate = health.SuccessfulReads / started.Elapsed.TotalSeconds;
var allocatedPerRead = allocatedBytes / (double)reads;
var cpuPercent = (process.TotalProcessorTime - cpuBefore).TotalMilliseconds /
    started.Elapsed.TotalMilliseconds /
    Math.Max(1, Environment.ProcessorCount) * 100;
var workingSetGrowth = Math.Max(0, process.WorkingSet64 - workingSetBefore);
var cadenceOk = actualRate >= options.PollRateHz * 0.8 &&
    actualRate <= options.PollRateHz * 1.15;
var allocationOk = allocatedPerRead <= options.MaximumAllocatedBytesPerRead;
var cpuOk = cpuPercent <= options.MaximumCpuPercent;
var readBudget = RuntimePerformanceBudget.Evaluate(health, process.WorkingSet64);
var passed = cadenceOk && allocationOk && cpuOk && readBudget.WithinBudget;

Console.WriteLine($"Duration: {started.Elapsed.TotalSeconds:0.0}s");
Console.WriteLine($"Reads: {health.SuccessfulReads} ({actualRate:0.0} Hz / {options.PollRateHz} Hz target)");
Console.WriteLine($"Read latency: avg {health.AverageReadMilliseconds:0.000} ms, max {health.MaximumReadMilliseconds:0.000} ms");
Console.WriteLine($"Allocation: {allocatedPerRead:0} bytes/read");
Console.WriteLine($"CPU: {cpuPercent:0.00}%");
Console.WriteLine($"Working-set growth: {workingSetGrowth / 1024d / 1024d:0.0} MB");
Console.WriteLine(passed ? "SOAK PASS" : "SOAK FAIL");

return passed ? 0 : 1;

internal sealed record SoakOptions(
    TimeSpan Duration,
    int PollRateHz,
    double MaximumAllocatedBytesPerRead,
    double MaximumCpuPercent)
{
    public static SoakOptions Parse(string[] arguments)
    {
        var durationSeconds = ReadInt(arguments, "--duration-seconds", 30, 1, 86400);
        var pollRate = ReadInt(arguments, "--poll-hz", 60, 10, 240);
        var allocation = ReadDouble(
            arguments,
            "--max-allocated-bytes-per-read",
            8192,
            256,
            1048576);
        var cpu = ReadDouble(arguments, "--max-cpu-percent", 15, 0.1, 100);
        return new(TimeSpan.FromSeconds(durationSeconds), pollRate, allocation, cpu);
    }

    private static int ReadInt(
        string[] arguments,
        string name,
        int fallback,
        int minimum,
        int maximum) =>
        int.TryParse(ReadValue(arguments, name), out var value)
            ? Math.Clamp(value, minimum, maximum)
            : fallback;

    private static double ReadDouble(
        string[] arguments,
        string name,
        double fallback,
        double minimum,
        double maximum) =>
        double.TryParse(
            ReadValue(arguments, name),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value)
                ? Math.Clamp(value, minimum, maximum)
                : fallback;

    private static string? ReadValue(string[] arguments, string name)
    {
        var index = Array.IndexOf(arguments, name);
        return index >= 0 && index + 1 < arguments.Length
            ? arguments[index + 1]
            : null;
    }
}

internal sealed class StableTelemetrySource : ILmuTelemetrySource
{
    private static readonly LmuTelemetrySnapshot Snapshot = new(
        LmuConnectionState.Connected,
        1,
        1,
        1,
        0,
        0,
        null,
        null,
        Array.Empty<LmuVehicleStanding>(),
        DateTimeOffset.UtcNow,
        string.Empty);

    public LmuProbeSnapshot ReadProbeSnapshot() =>
        LmuProbeSnapshot.Disconnected("soak fixture");

    public LmuTelemetrySnapshot ReadTelemetrySnapshot() =>
        Snapshot with { CapturedAt = DateTimeOffset.UtcNow };

    public void Dispose()
    {
    }
}
