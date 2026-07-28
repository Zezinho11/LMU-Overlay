using System.Text.Json;
using System.Text.Json.Serialization;
using LmuOverlay.Domain;
using LmuOverlay.LmuSharedMemory;

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("The LMU shared-memory probe requires Windows.");
    return 2;
}

using var reader = new LmuSharedMemoryReader();
var snapshot = reader.ReadTelemetrySnapshot();
var output = new
{
    Snapshot = snapshot,
    Metrics = LmuTelemetryMetricsCalculator.Calculate(snapshot)
};
Console.WriteLine(JsonSerializer.Serialize(output, new JsonSerializerOptions
{
    WriteIndented = true,
    Converters = { new JsonStringEnumConverter() }
}));

return snapshot.State == LmuConnectionState.Connected ? 0 : 1;
