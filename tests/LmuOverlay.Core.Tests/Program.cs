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

Console.WriteLine("Core checks passed.");
return 0;

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
