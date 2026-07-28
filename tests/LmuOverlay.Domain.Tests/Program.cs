using LmuOverlay.Domain;

var probe = LmuProbeSnapshot.Disconnected("not running");
Require(probe.State == LmuConnectionState.Disconnected, "Disconnected state");
Require(probe.Detail == "not running", "Disconnected detail");
Require(!probe.HasPlayerVehicle, "Disconnected player flag");

var unavailable = LmuTelemetrySnapshot.Unavailable(
    LmuConnectionState.Disconnected,
    "not running");
Require(
    LmuTelemetryMetricsCalculator.Calculate(unavailable) == LmuTelemetryMetrics.Empty,
    "Unavailable metrics");

var vector = new LmuVector3(3, 0, 4);
Require(vector.Length == 5, "Vector length");

Console.WriteLine("Domain checks passed.");
return 0;

static void Require(bool condition, string name)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Check failed: {name}");
    }
}
