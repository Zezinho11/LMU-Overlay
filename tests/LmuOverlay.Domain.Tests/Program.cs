using LmuOverlay.Domain;

var snapshot = LmuProbeSnapshot.Disconnected("not running");
Require(snapshot.State == LmuConnectionState.Disconnected, "Disconnected state");
Require(snapshot.Detail == "not running", "Disconnected detail");
Require(!snapshot.HasPlayerVehicle, "Disconnected player flag");

Console.WriteLine("Domain checks passed.");
return 0;

static void Require(bool condition, string name)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Check failed: {name}");
    }
}
