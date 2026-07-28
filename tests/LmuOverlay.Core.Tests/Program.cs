using LmuOverlay.Contracts;
using LmuOverlay.Core;
using LmuOverlay.Domain;

using var source = new FakeTelemetrySource();
var polling = new TelemetryPollingService(source, TimeSpan.FromMilliseconds(10));
await using var enumerator = polling.WatchAsync().GetAsyncEnumerator();

Require(await enumerator.MoveNextAsync(), "Initial frame available");
Require(enumerator.Current.State == LmuConnectionState.Disconnected, "Initial frame state");
Require(source.ReadCount == 1, "Source read once");

Console.WriteLine("Core checks passed.");
return 0;

static void Require(bool condition, string name)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Check failed: {name}");
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
