using System.Globalization;
using LmuOverlay.Domain;

namespace LmuOverlay.LmuSharedMemory;

public static partial class LmuSnapshotParser
{
    private static LmuVehicleStanding[] ParseStandings(
        ReadOnlySpan<byte> data,
        int vehicleCount,
        IReadOnlyDictionary<int, VehicleMetadata> vehicleMetadata)
    {
        var standings = new LmuVehicleStanding[vehicleCount];
        for (var index = 0; index < vehicleCount; index++)
        {
            var offset = LmuApiLayoutV1.VehicleScoringOffset(index);
            var vehicleId = ReadInt32(
                data,
                offset + LmuApiLayoutV1.ScoringVehicleIdOffset);
            vehicleMetadata.TryGetValue(vehicleId, out var metadata);
            standings[index] = new(
                vehicleId,
                ReadText(
                    data,
                    offset + LmuApiLayoutV1.ScoringDriverNameOffset,
                    LmuApiLayoutV1.ScoringDriverNameLength),
                ReadText(
                    data,
                    offset + LmuApiLayoutV1.ScoringVehicleNameOffset,
                    LmuApiLayoutV1.ScoringVehicleNameLength),
                metadata?.VehicleModel ?? string.Empty,
                ReadText(
                    data,
                    offset + LmuApiLayoutV1.ScoringVehicleClassOffset,
                    LmuApiLayoutV1.ScoringVehicleClassLength),
                data[offset + LmuApiLayoutV1.ScoringPositionOffset],
                ReadInt16(data, offset + LmuApiLayoutV1.ScoringCompletedLapsOffset),
                unchecked((sbyte)data[offset + LmuApiLayoutV1.ScoringSectorOffset]),
                ReadDouble(data, offset + LmuApiLayoutV1.ScoringLapDistanceOffset),
                ReadDouble(data, offset + LmuApiLayoutV1.ScoringBestLapTimeOffset),
                ReadDouble(data, offset + LmuApiLayoutV1.ScoringLastLapTimeOffset),
                ReadDouble(data, offset + LmuApiLayoutV1.ScoringGapToNextOffset),
                ReadInt32(data, offset + LmuApiLayoutV1.ScoringLapsBehindNextOffset),
                ReadDouble(data, offset + LmuApiLayoutV1.ScoringGapToLeaderOffset),
                ReadInt32(data, offset + LmuApiLayoutV1.ScoringLapsBehindLeaderOffset),
                ReadInt16(data, offset + LmuApiLayoutV1.ScoringPitStopsOffset),
                ReadInt16(data, offset + LmuApiLayoutV1.ScoringPenaltiesOffset),
                ReadBoolean(data, offset + LmuApiLayoutV1.VehicleScoringIsPlayerOffset),
                ReadBoolean(data, offset + LmuApiLayoutV1.ScoringInPitsOffset),
                (LmuPitState)data[offset + LmuApiLayoutV1.ScoringPitStateOffset],
                data[offset + LmuApiLayoutV1.ScoringFlagOffset],
                ReadBoolean(data, offset + LmuApiLayoutV1.ScoringUnderYellowOffset),
                ReadBoolean(data, offset + LmuApiLayoutV1.ScoringInGarageOffset),
                data[offset + LmuApiLayoutV1.ScoringFuelFractionOffset] / 255d,
                ReadBoolean(data, offset + LmuApiLayoutV1.ScoringDrsActiveOffset),
                ReadDouble(data, offset + LmuApiLayoutV1.ScoringBestSector1Offset),
                ReadDouble(data, offset + LmuApiLayoutV1.ScoringBestSector2Offset),
                ReadDouble(data, offset + LmuApiLayoutV1.ScoringLastSector1Offset),
                ReadDouble(data, offset + LmuApiLayoutV1.ScoringLastSector2Offset),
                ReadDouble(data, offset + LmuApiLayoutV1.ScoringCurrentSector1Offset),
                ReadDouble(data, offset + LmuApiLayoutV1.ScoringCurrentSector2Offset),
                metadata?.FrontTireCompound ?? string.Empty,
                metadata?.RearTireCompound ?? string.Empty,
                metadata?.FrontTireCompoundIndex ?? 0,
                metadata?.RearTireCompoundIndex ?? 0,
                metadata?.VirtualEnergyFraction ?? -1,
                ReadSingle(data, offset + LmuApiLayoutV1.ScoringBestLapSector1Offset),
                ReadSingle(data, offset + LmuApiLayoutV1.ScoringBestLapSector2Offset));
        }

        return standings;
    }
}
