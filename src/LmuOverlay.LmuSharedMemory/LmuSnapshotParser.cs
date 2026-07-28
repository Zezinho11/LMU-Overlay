using System.Buffers.Binary;
using System.Text;
using LmuOverlay.Domain;

namespace LmuOverlay.LmuSharedMemory;

public static class LmuSnapshotParser
{
    public static LmuProbeSnapshot Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < LmuApiLayoutV1.ObjectSize)
        {
            return new(
                LmuConnectionState.IncompatibleLayout,
                string.Empty,
                string.Empty,
                0,
                string.Empty,
                0,
                0,
                false,
                DateTimeOffset.UtcNow,
                $"Expected at least {LmuApiLayoutV1.ObjectSize} bytes, received {data.Length}.");
        }

        var activeVehicles = data[LmuApiLayoutV1.ActiveVehiclesOffset];
        var scoredVehicles = ReadInt32(data, LmuApiLayoutV1.ScoredVehiclesOffset);
        var playerVehicleIndex = data[LmuApiLayoutV1.PlayerVehicleIndexOffset];
        var hasPlayerVehicle = data[LmuApiLayoutV1.PlayerHasVehicleOffset] != 0;

        if (activeVehicles > LmuApiLayoutV1.MaximumVehicles ||
            scoredVehicles is < 0 or > LmuApiLayoutV1.MaximumVehicles)
        {
            return new(
                LmuConnectionState.InvalidData,
                ReadNullTerminatedUtf8(data, LmuApiLayoutV1.GameVersionOffset, LmuApiLayoutV1.GameVersionLength),
                string.Empty,
                0,
                string.Empty,
                activeVehicles,
                scoredVehicles,
                hasPlayerVehicle,
                DateTimeOffset.UtcNow,
                "Vehicle counts are outside the supported LMU layout range.");
        }

        var playerVehicleName = string.Empty;
        if (hasPlayerVehicle && playerVehicleIndex < activeVehicles)
        {
            var vehicleOffset = LmuApiLayoutV1.VehicleTelemetryOffset(playerVehicleIndex);
            playerVehicleName = ReadNullTerminatedUtf8(
                data,
                vehicleOffset + LmuApiLayoutV1.VehicleNameOffset,
                LmuApiLayoutV1.VehicleNameLength);
        }

        return new(
            LmuConnectionState.Connected,
            ReadNullTerminatedUtf8(data, LmuApiLayoutV1.GameVersionOffset, LmuApiLayoutV1.GameVersionLength),
            ReadNullTerminatedUtf8(data, LmuApiLayoutV1.TrackNameOffset, LmuApiLayoutV1.TrackNameLength),
            ReadInt32(data, LmuApiLayoutV1.SessionCodeOffset),
            playerVehicleName,
            activeVehicles,
            scoredVehicles,
            hasPlayerVehicle,
            DateTimeOffset.UtcNow,
            "Read-only LMU shared-memory snapshot parsed successfully.");
    }

    private static int ReadInt32(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, sizeof(int)));

    private static string ReadNullTerminatedUtf8(ReadOnlySpan<byte> data, int offset, int length)
    {
        var field = data.Slice(offset, length);
        var terminator = field.IndexOf((byte)0);
        if (terminator >= 0)
        {
            field = field[..terminator];
        }

        return Encoding.UTF8.GetString(field).Trim();
    }
}
