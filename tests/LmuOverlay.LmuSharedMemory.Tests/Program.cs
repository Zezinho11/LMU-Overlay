using System.Buffers.Binary;
using System.Text;
using LmuOverlay.Domain;
using LmuOverlay.LmuSharedMemory;

var data = new byte[LmuApiLayoutV1.ObjectSize];
WriteText(data, LmuApiLayoutV1.GameVersionOffset, "1.2");
WriteText(data, LmuApiLayoutV1.TrackNameOffset, "Le Mans");
BinaryPrimitives.WriteInt32LittleEndian(
    data.AsSpan(LmuApiLayoutV1.SessionCodeOffset, sizeof(int)),
    10);
BinaryPrimitives.WriteInt32LittleEndian(
    data.AsSpan(LmuApiLayoutV1.ScoredVehiclesOffset, sizeof(int)),
    2);
data[LmuApiLayoutV1.ActiveVehiclesOffset] = 2;
data[LmuApiLayoutV1.PlayerVehicleIndexOffset] = 1;
data[LmuApiLayoutV1.PlayerHasVehicleOffset] = 1;
WriteText(
    data,
    LmuApiLayoutV1.VehicleTelemetryOffset(1) + LmuApiLayoutV1.VehicleNameOffset,
    "Porsche 963");

var snapshot = LmuSnapshotParser.Parse(data);
Require(snapshot.State == LmuConnectionState.Connected, "Connected state");
Require(snapshot.GameVersion == "1.2", "Game version");
Require(snapshot.TrackName == "Le Mans", "Track name");
Require(snapshot.SessionCode == 10, "Session code");
Require(snapshot.PlayerVehicleName == "Porsche 963", "Player vehicle");
Require(snapshot.ActiveVehicles == 2, "Active vehicle count");
Require(snapshot.ScoredVehicles == 2, "Scored vehicle count");
Require(snapshot.HasPlayerVehicle, "Player vehicle flag");

var shortSnapshot = LmuSnapshotParser.Parse(new byte[128]);
Require(shortSnapshot.State == LmuConnectionState.IncompatibleLayout, "Short layout");

var invalidCounts = new byte[LmuApiLayoutV1.ObjectSize];
BinaryPrimitives.WriteInt32LittleEndian(
    invalidCounts.AsSpan(LmuApiLayoutV1.ScoredVehiclesOffset, sizeof(int)),
    LmuApiLayoutV1.MaximumVehicles + 1);
var invalidSnapshot = LmuSnapshotParser.Parse(invalidCounts);
Require(invalidSnapshot.State == LmuConnectionState.InvalidData, "Invalid counts");

Console.WriteLine("LMU shared-memory checks passed.");
return 0;

static void WriteText(byte[] destination, int offset, string value) =>
    Encoding.UTF8.GetBytes(value, destination.AsSpan(offset));

static void Require(bool condition, string name)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Check failed: {name}");
    }
}
