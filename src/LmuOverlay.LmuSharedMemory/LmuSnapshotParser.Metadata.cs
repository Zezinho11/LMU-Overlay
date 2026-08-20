using System.Globalization;
using LmuOverlay.Domain;

namespace LmuOverlay.LmuSharedMemory;

public static partial class LmuSnapshotParser
{
    private static IReadOnlyDictionary<int, VehicleMetadata> ParseVehicleMetadata(
        ReadOnlySpan<byte> data,
        int vehicleCount)
    {
        var models = new Dictionary<int, VehicleMetadata>(vehicleCount);
        for (var index = 0; index < vehicleCount; index++)
        {
            var offset = LmuApiLayoutV1.VehicleTelemetryOffset(index);
            var vehicleId = ReadInt32(
                data,
                offset + LmuApiLayoutV1.TelemetryVehicleIdOffset);
            var vehicleName = ReadText(
                data,
                offset + LmuApiLayoutV1.VehicleNameOffset,
                LmuApiLayoutV1.VehicleNameLength);
            var vehicleModel = ReadText(
                data,
                offset + LmuApiLayoutV1.TelemetryVehicleModelOffset,
                LmuApiLayoutV1.TelemetryVehicleModelLength);
            models[vehicleId] = new(
                string.Join(
                    " ",
                    new[] { vehicleName, vehicleModel }
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Distinct(StringComparer.OrdinalIgnoreCase)),
                ReadText(
                    data,
                    offset + LmuApiLayoutV1.TelemetryFrontTireCompoundNameOffset,
                    LmuApiLayoutV1.TelemetryTireCompoundNameLength),
                ReadText(
                    data,
                    offset + LmuApiLayoutV1.TelemetryRearTireCompoundNameOffset,
                    LmuApiLayoutV1.TelemetryTireCompoundNameLength),
                data[offset + LmuApiLayoutV1.TelemetryFrontTireCompoundIndexOffset],
                data[offset + LmuApiLayoutV1.TelemetryRearTireCompoundIndexOffset],
                ReadSingle(data, offset + LmuApiLayoutV1.TelemetryVirtualEnergyOffset));
        }

        return models;
    }

    private sealed record VehicleMetadata(
        string VehicleModel,
        string FrontTireCompound,
        string RearTireCompound,
        int FrontTireCompoundIndex,
        int RearTireCompoundIndex,
        double VirtualEnergyFraction);
}
