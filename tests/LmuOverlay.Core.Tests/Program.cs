using LmuOverlay.Contracts;
using LmuOverlay.Core;
using LmuOverlay.Domain;

using var source = new FakeTelemetrySource();
var polling = new TelemetryPollingService(source, TimeSpan.FromMilliseconds(10));
await using var enumerator = polling.WatchAsync().GetAsyncEnumerator();

Require(await enumerator.MoveNextAsync(), "Initial frame available");
Require(enumerator.Current.State == LmuConnectionState.Disconnected, "Initial frame state");
Require(source.ReadCount == 1, "Source read once");

var runtimeSource = new FakeTelemetrySource();
await using (var runtime = new TelemetryRuntime(
    () => runtimeSource,
    TimeSpan.FromMilliseconds(5),
    TimeSpan.FromMilliseconds(10)))
{
    runtime.Start();
    await WaitUntilAsync(
        () => runtime.Health.FailedReads >= 2,
        TimeSpan.FromSeconds(1));
    Require(runtime.Health.Reconnects >= 2, "Runtime reconnects after unavailable reads");
    Require(runtime.Latest.State == LmuConnectionState.Disconnected, "Runtime publishes latest frame");
    Require(runtime.Health.LastReadMilliseconds >= 0, "Runtime records read duration");
    Require(runtime.Health.P99ReadMilliseconds >= 0,
        "Runtime exposes bounded p99 read latency.");
    Require(runtime.Health.StaleAgeMilliseconds >= 0,
        "Runtime exposes the age of the last successful sample.");
}

var healthyBudget = RuntimePerformanceBudget.Evaluate(
    new TelemetryRuntimeHealth(
        100, 0, 1, 1, 2, 8, DateTimeOffset.UtcNow, string.Empty),
    120L * 1024 * 1024);
Require(healthyBudget.WithinBudget, "Healthy runtime must pass performance budgets.");
var slowBudget = RuntimePerformanceBudget.Evaluate(
    new TelemetryRuntimeHealth(
        100, 0, 1, 5, 6, 30, DateTimeOffset.UtcNow, string.Empty),
    300L * 1024 * 1024);
Require(!slowBudget.WithinBudget, "Slow and oversized runtime must fail budgets.");

Require(
    TelemetryRuntime.AdvanceDeadline(
        previousDeadline: 100,
        attemptCompleted: 108,
        waitTicks: 20) == 120,
    "Read time must not be added to every polling interval.");
Require(
    TelemetryRuntime.AdvanceDeadline(
        previousDeadline: 100,
        attemptCompleted: 125,
        waitTicks: 20) == 145,
    "A missed polling deadline must resume without building a backlog.");

var eventSource = new WaitableHealthyTelemetrySource();
await using (var eventRuntime = new TelemetryRuntime(
    () => eventSource,
    TimeSpan.FromMilliseconds(100),
    TimeSpan.FromMilliseconds(100)))
{
    eventRuntime.Start();
    await WaitUntilAsync(
        () => eventRuntime.Health.SuccessfulReads >= 1,
        TimeSpan.FromSeconds(1));
    eventSource.Signal();
    await WaitUntilAsync(
        () => eventRuntime.Health.EventWakeups >= 1 &&
              eventRuntime.Health.SuccessfulReads >= 2,
        TimeSpan.FromSeconds(1));
    Require(eventRuntime.Health.SuccessfulReads >= 2,
        "Named event wake must trigger an immediate telemetry read.");
    Require(eventRuntime.Health.PublishedSnapshots >= 1,
        "Runtime must expose publication metrics.");
}

// Exercise the shutdown race repeatedly: cancellation commonly arrives while
// the polling worker is sleeping between reconnect attempts. That is a normal
// lifecycle transition and must never terminate the process.
for (var iteration = 0; iteration < 25; iteration++)
{
    var shutdownRuntime = new TelemetryRuntime(
        () => new FakeTelemetrySource(),
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(1));
    shutdownRuntime.Start();
    await Task.Delay(1);
    await shutdownRuntime.DisposeAsync();
}

var recordingPath = Path.Combine(
    Path.GetTempPath(),
    $"lmu-overlay-recording-{Guid.NewGuid():N}.lmu-replay");
try
{
    await using (var recordingWriter =
        await TelemetryRecordingWriter.CreateAsync(recordingPath, capacity: 8))
    {
        Require(recordingWriter.TryRecord(RecordingSnapshot(41)),
            "First normalized snapshot must enter the recording buffer.");
        Require(recordingWriter.TryRecord(RecordingSnapshot(42)),
            "Second normalized snapshot must enter the recording buffer.");
    }

    var recording = await TelemetryRecordingReader.ReadAsync(recordingPath);
    Require(recording.Header.SchemaVersion ==
        TelemetryRecordingWriter.CurrentSchemaVersion,
        "Recorder schema must be explicit and versioned.");
    Require(recording.Header.Anonymized,
        "Recorder must mark fixtures as anonymized.");
    Require(recording.Frames.Count == 2,
        "Recorder must preserve a sequential normalized snapshot stream.");
    var recorded = recording.Frames[0].Snapshot;
    Require(recorded.Session?.PlayerName == "Driver 01",
        "Player identity must be replaced with a deterministic alias.");
    Require(recorded.Player?.VehicleId == 1 &&
            recorded.Player.VehicleName == "Car 01",
        "Player car identity must be remapped without losing telemetry.");
    Require(recorded.Standings[0].DriverName == "Driver 01" &&
            recorded.Standings[1].DriverName == "Driver 02",
        "Standing identities must be stable inside one recording.");
    Require(recorded.Standings[0].VehicleModel == "Porsche 963",
        "Technical model data must remain available for regression playback.");
    Require(string.IsNullOrEmpty(recorded.Detail),
        "Potentially sensitive runtime details must not be recorded.");

    using var replay = new ReplayTelemetrySource(recording, speed: 1000);
    Require(replay.ReadTelemetrySnapshot().TelemetrySequence == 41,
        "Replay must begin with the first recorded frame.");
    using var replayCancellation = new ManualResetEvent(false);
    Require(replay.WaitForUpdate(replayCancellation, TimeSpan.FromSeconds(1)) ==
            TelemetryUpdateWaitResult.Signaled,
        "Replay must preserve frame timing and signal the next frame.");
    Require(replay.ReadTelemetrySnapshot().TelemetrySequence == 42,
        "Replay must deliver frames in deterministic order.");
}
finally
{
    File.Delete(recordingPath);
}

var committedReplay = await TelemetryRecordingReader.ReadAsync(Path.Combine(
    AppContext.BaseDirectory,
    "fixtures",
    "anonymous-sequence.lmu-replay"));
Require(committedReplay.Frames.Count == 3 &&
        committedReplay.Frames[^1].Snapshot.TelemetrySequence == 103,
    "Committed sequential replay fixture must remain readable.");

Console.WriteLine("Core checks passed.");
return 0;

static LmuTelemetrySnapshot RecordingSnapshot(uint telemetrySequence)
{
    var session = new LmuSessionSnapshot(
        "Circuit de Spa-Francorchamps", 1, LmuSessionKind.Practice,
        LmuGamePhase.GreenFlag, 100, 3600, 0, 7004, true, "José Suzuki",
        new LmuWeatherSnapshot(0.2, 0, 20, 28, default, 0, 0, 0, 2));
    var player = new LmuPlayerTelemetry(
        VehicleId: 77,
        VehicleName: "RedFox #77",
        VehicleModel: "Porsche 963",
        VehicleClassId: 1,
        LapNumber: 3,
        LapStartElapsedTime: 80,
        LapDistanceMeters: 1200,
        Position: 1,
        Gear: 4,
        MaximumGears: 7,
        EngineRpm: 7200,
        EngineMaximumRpm: 9000,
        EngineWaterTemperatureCelsius: 78,
        EngineOilTemperatureCelsius: 90,
        RearBrakeBiasFraction: 0.51,
        SpeedKilometersPerHour: 220,
        Throttle: 1,
        Brake: 0,
        Steering: 0.1,
        Clutch: 0,
        FuelLiters: 50,
        FuelCapacityLiters: 100,
        DeltaBestSeconds: -0.1,
        BatteryChargeFraction: 0.5,
        StateOfCharge: 0.5,
        RegenerationKilowatts: 0,
        VirtualEnergy: 0.8,
        GapToCarAheadSeconds: 0,
        GapToCarBehindSeconds: 1,
        CurrentSector: 2,
        SpeedLimiterActive: false,
        LapInvalidated: false,
        AbsActive: false,
        TractionControlActive: false,
        TractionControlLevel: 3,
        TractionControlMaximum: 12,
        TractionControlSlipLevel: 3,
        TractionControlSlipMaximum: 12,
        TractionControlCutLevel: 3,
        TractionControlCutMaximum: 12,
        AbsLevel: 4,
        AbsMaximum: 12,
        TireTemperatures: new LmuWheelTemperatures(80, 81, 79, 80),
        TireWear: new LmuWheelWear(0.1, 0.1, 0.1, 0.1));
    var standings = new[]
    {
        RecordingStanding(77, "José Suzuki", "RedFox #77", 1, true),
        RecordingStanding(12, "Real Rival", "Private Team #12", 2, false),
    };
    return new LmuTelemetrySnapshot(
        LmuConnectionState.Connected, 14000, telemetrySequence,
        telemetrySequence, 2, 2, session, player, standings,
        DateTimeOffset.UtcNow, "C:\\Users\\private\\diagnostics.log");
}

static LmuVehicleStanding RecordingStanding(
    int vehicleId,
    string driver,
    string vehicle,
    int position,
    bool isPlayer) => new(
        vehicleId, driver, vehicle, "Porsche 963", "Hypercar", position,
        3, 2, 1200, 112, 113, 1, 0, position - 1, 0, 0, 0,
        isPlayer, false, LmuPitState.None, 0, false, false, 0.5, false);

static void Require(bool condition, string name)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Check failed: {name}");
    }
}

static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
{
    var deadline = DateTimeOffset.UtcNow + timeout;
    while (!condition())
    {
        if (DateTimeOffset.UtcNow >= deadline)
        {
            throw new TimeoutException("Timed out waiting for telemetry runtime.");
        }

        await Task.Delay(5);
    }
}

internal sealed class FakeTelemetrySource : ILmuTelemetrySource
{
    public int ReadCount { get; private set; }

    public LmuProbeSnapshot ReadProbeSnapshot() =>
        LmuProbeSnapshot.Disconnected("fixture");

    public LmuTelemetrySnapshot ReadTelemetrySnapshot()
    {
        ReadCount++;
        return LmuTelemetrySnapshot.Unavailable(
            LmuConnectionState.Disconnected,
            "fixture");
    }

    public void Dispose()
    {
    }
}

internal sealed class WaitableHealthyTelemetrySource :
    ILmuTelemetrySource,
    IWaitableTelemetrySource
{
    private readonly AutoResetEvent _update = new(false);
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

    public void Signal() => _update.Set();

    public TelemetryUpdateWaitResult WaitForUpdate(
        WaitHandle cancellation,
        TimeSpan timeout) =>
        WaitHandle.WaitAny([cancellation, _update], timeout) switch
        {
            0 => TelemetryUpdateWaitResult.Cancelled,
            1 => TelemetryUpdateWaitResult.Signaled,
            _ => TelemetryUpdateWaitResult.TimedOut,
        };

    public LmuProbeSnapshot ReadProbeSnapshot() =>
        LmuProbeSnapshot.Disconnected("event fixture");

    public LmuTelemetrySnapshot ReadTelemetrySnapshot() => Snapshot;

    public void Dispose() => _update.Dispose();
}
