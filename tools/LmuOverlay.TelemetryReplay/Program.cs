using LmuOverlay.Core;
using LmuOverlay.LmuSharedMemory;
using System.Runtime.Versioning;

if (args.Length == 0)
{
    PrintUsage();
    return 2;
}

try
{
    return args[0].ToLowerInvariant() switch
    {
        "record" => OperatingSystem.IsWindows()
            ? await RecordAsync(args.Skip(1).ToArray())
            : WindowsRequired(),
        "inspect" => await InspectAsync(args.Skip(1).ToArray()),
        "play" => await PlayAsync(args.Skip(1).ToArray()),
        _ => UnknownCommand(),
    };
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

[SupportedOSPlatform("windows")]
static async Task<int> RecordAsync(string[] arguments)
{
    if (!OperatingSystem.IsWindows())
    {
        Console.Error.WriteLine("Live recording requires Windows.");
        return 2;
    }

    var output = Value(arguments, "--output") ??
        Path.Combine(Environment.CurrentDirectory, $"lmu-{DateTime.UtcNow:yyyyMMdd-HHmmss}.lmu-replay");
    var duration = PositiveDouble(Value(arguments, "--duration-seconds"), 60);
    await using var writer = await TelemetryRecordingWriter.CreateAsync(output);
    await using var runtime = new TelemetryRuntime(
        CreateLiveSource,
        TimeSpan.FromMilliseconds(8),
        TimeSpan.FromSeconds(1));
    void Capture(LmuOverlay.Domain.LmuTelemetrySnapshot snapshot) =>
        writer.TryRecord(snapshot);
    runtime.SnapshotPublished += Capture;
    runtime.Start();
    Console.WriteLine($"Recording anonymized telemetry for {duration:0.#} seconds...");
    await Task.Delay(TimeSpan.FromSeconds(duration));
    runtime.SnapshotPublished -= Capture;
    Console.WriteLine($"Saved: {Path.GetFullPath(output)}");
    Console.WriteLine($"Dropped frames: {writer.DroppedFrames}");
    return 0;
}

[SupportedOSPlatform("windows")]
static LmuOverlay.Contracts.ILmuTelemetrySource CreateLiveSource() =>
    new LmuSharedMemoryReader();

static async Task<int> InspectAsync(string[] arguments)
{
    var input = RequiredValue(arguments, "--input");
    var recording = await TelemetryRecordingReader.ReadAsync(input);
    var first = recording.Frames.FirstOrDefault()?.Snapshot;
    var last = recording.Frames.LastOrDefault()?.Snapshot;
    Console.WriteLine($"Schema: {recording.Header.SchemaVersion}");
    Console.WriteLine($"Anonymized: {recording.Header.Anonymized}");
    Console.WriteLine($"Frames: {recording.Frames.Count}");
    Console.WriteLine($"Track: {first?.Session?.TrackName ?? "--"}");
    Console.WriteLine($"Session: {first?.Session?.Kind.ToString() ?? "--"}");
    Console.WriteLine($"Duration: {((recording.Frames.LastOrDefault()?.OffsetMicroseconds ?? 0) / 1_000_000d):0.000}s");
    Console.WriteLine($"Sequences: {first?.TelemetrySequence ?? 0} -> {last?.TelemetrySequence ?? 0}");
    return 0;
}

static async Task<int> PlayAsync(string[] arguments)
{
    var input = RequiredValue(arguments, "--input");
    var speed = PositiveDouble(Value(arguments, "--speed"), 1);
    var recording = await TelemetryRecordingReader.ReadAsync(input);
    using var replay = new ReplayTelemetrySource(recording, speed);
    using var cancellation = new ManualResetEvent(false);
    while (replay.RemainingFrames > 0)
    {
        if (replay.WaitForUpdate(cancellation, TimeSpan.FromSeconds(1)) ==
            LmuOverlay.Contracts.TelemetryUpdateWaitResult.Signaled)
        {
            var snapshot = replay.ReadTelemetrySnapshot();
            Console.WriteLine(
                $"{snapshot.TelemetrySequence,10}  " +
                $"lap {snapshot.Player?.LapNumber + 1,3}  " +
                $"{snapshot.Player?.SpeedKilometersPerHour,6:0.0} km/h  " +
                $"{snapshot.Session?.TrackName ?? "--"}");
        }
    }

    return 0;
}

static string RequiredValue(string[] arguments, string name) =>
    Value(arguments, name) ?? throw new ArgumentException($"Missing {name}.");

static string? Value(string[] arguments, string name)
{
    var index = Array.IndexOf(arguments, name);
    return index >= 0 && index + 1 < arguments.Length
        ? arguments[index + 1]
        : null;
}

static double PositiveDouble(string? value, double fallback) =>
    double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var parsed) &&
    double.IsFinite(parsed) && parsed > 0
        ? parsed
        : fallback;

static int UnknownCommand()
{
    PrintUsage();
    return 2;
}

static int WindowsRequired()
{
    Console.Error.WriteLine("Live recording requires Windows.");
    return 2;
}

static void PrintUsage()
{
    Console.WriteLine("LMU Overlay telemetry recorder/replay");
    Console.WriteLine("  record  [--output file] [--duration-seconds 60]");
    Console.WriteLine("  inspect --input file");
    Console.WriteLine("  play    --input file [--speed 1]");
}
