using System.Globalization;
using LmuOverlay.Domain;

namespace LmuOverlay.LmuSharedMemory;

public static partial class LmuSnapshotParser
{
    private static LmuPlayerTelemetry ParsePlayer(
        ReadOnlySpan<byte> data,
        int playerVehicleIndex,
        LmuVehicleStanding? standing,
        LmuPlayerTelemetry? metadataSource = null)
    {
        var offset = LmuApiLayoutV1.VehicleTelemetryOffset(playerVehicleIndex);
        return ParsePlayerAtOffset(data, offset, standing, metadataSource);
    }

    private static LmuPlayerTelemetry ParsePlayerAtOffset(
        ReadOnlySpan<byte> data,
        int offset,
        LmuVehicleStanding? standing,
        LmuPlayerTelemetry? metadataSource = null)
    {
        var velocity = ReadVector3(
            data,
            offset + LmuApiLayoutV1.TelemetryLocalVelocityOffset);

        return new(
            ReadInt32(data, offset + LmuApiLayoutV1.TelemetryVehicleIdOffset),
            metadataSource?.VehicleName ?? ReadText(
                data,
                offset + LmuApiLayoutV1.VehicleNameOffset,
                LmuApiLayoutV1.VehicleNameLength),
            metadataSource?.VehicleModel ?? ReadText(
                data,
                offset + LmuApiLayoutV1.TelemetryVehicleModelOffset,
                LmuApiLayoutV1.TelemetryVehicleModelLength),
            data[offset + LmuApiLayoutV1.TelemetryVehicleClassOffset],
            ReadInt32(data, offset + LmuApiLayoutV1.TelemetryLapNumberOffset),
            ReadDouble(data, offset + LmuApiLayoutV1.TelemetryLapStartElapsedTimeOffset),
            standing?.LapDistanceMeters ?? 0,
            standing?.Position ?? 0,
            ReadInt32(data, offset + LmuApiLayoutV1.TelemetryGearOffset),
            data[offset + LmuApiLayoutV1.TelemetryMaximumGearsOffset],
            ReadDouble(data, offset + LmuApiLayoutV1.TelemetryEngineRpmOffset),
            ReadDouble(data, offset + LmuApiLayoutV1.TelemetryEngineMaximumRpmOffset),
            ReadDouble(
                data,
                offset + LmuApiLayoutV1.TelemetryEngineWaterTemperatureOffset),
            ReadDouble(
                data,
                offset + LmuApiLayoutV1.TelemetryEngineOilTemperatureOffset),
            ReadDouble(data, offset + LmuApiLayoutV1.TelemetryRearBrakeBiasOffset),
            velocity.Length * 3.6,
            ReadDouble(data, offset + LmuApiLayoutV1.TelemetryThrottleOffset),
            ReadDouble(data, offset + LmuApiLayoutV1.TelemetryBrakeOffset),
            ReadDouble(data, offset + LmuApiLayoutV1.TelemetrySteeringOffset),
            ReadDouble(data, offset + LmuApiLayoutV1.TelemetryClutchOffset),
            ReadDouble(data, offset + LmuApiLayoutV1.TelemetryFuelOffset),
            ReadDouble(data, offset + LmuApiLayoutV1.TelemetryFuelCapacityOffset),
            ReadDouble(data, offset + LmuApiLayoutV1.TelemetryDeltaBestOffset),
            ReadDouble(data, offset + LmuApiLayoutV1.TelemetryBatteryChargeOffset),
            ReadSingle(data, offset + LmuApiLayoutV1.TelemetryStateOfChargeOffset),
            ReadSingle(data, offset + LmuApiLayoutV1.TelemetryRegenerationOffset),
            ReadSingle(data, offset + LmuApiLayoutV1.TelemetryVirtualEnergyOffset),
            ReadSingle(data, offset + LmuApiLayoutV1.TelemetryGapToCarAheadOffset),
            ReadSingle(data, offset + LmuApiLayoutV1.TelemetryGapToCarBehindOffset),
            ReadInt32(data, offset + LmuApiLayoutV1.TelemetryCurrentSectorOffset),
            ReadBoolean(data, offset + LmuApiLayoutV1.TelemetrySpeedLimiterActiveOffset),
            ReadBoolean(data, offset + LmuApiLayoutV1.TelemetryLapInvalidatedOffset),
            ReadBoolean(data, offset + LmuApiLayoutV1.TelemetryAbsActiveOffset),
            ReadBoolean(data, offset + LmuApiLayoutV1.TelemetryTractionControlActiveOffset),
            data[offset + LmuApiLayoutV1.TelemetryTractionControlLevelOffset],
            data[offset + LmuApiLayoutV1.TelemetryTractionControlMaximumOffset],
            data[offset + LmuApiLayoutV1.TelemetryTractionControlSlipLevelOffset],
            data[offset + LmuApiLayoutV1.TelemetryTractionControlSlipMaximumOffset],
            data[offset + LmuApiLayoutV1.TelemetryTractionControlCutLevelOffset],
            data[offset + LmuApiLayoutV1.TelemetryTractionControlCutMaximumOffset],
            data[offset + LmuApiLayoutV1.TelemetryAbsLevelOffset],
            data[offset + LmuApiLayoutV1.TelemetryAbsMaximumOffset],
            new LmuWheelTemperatures(
                ReadWheelTemperature(data, offset, 0),
                ReadWheelTemperature(data, offset, 1),
                ReadWheelTemperature(data, offset, 2),
                ReadWheelTemperature(data, offset, 3)),
            new LmuWheelWear(
                ReadWheelWear(data, offset, 0),
                ReadWheelWear(data, offset, 1),
                ReadWheelWear(data, offset, 2),
                ReadWheelWear(data, offset, 3)),
            metadataSource?.Damage ?? ParseDamage(data, offset),
            metadataSource?.FrontTireCompound ?? ReadText(
                data,
                offset + LmuApiLayoutV1.TelemetryFrontTireCompoundNameOffset,
                LmuApiLayoutV1.TelemetryTireCompoundNameLength),
            metadataSource?.RearTireCompound ?? ReadText(
                data,
                offset + LmuApiLayoutV1.TelemetryRearTireCompoundNameOffset,
                LmuApiLayoutV1.TelemetryTireCompoundNameLength),
            data[offset + LmuApiLayoutV1.TelemetryFrontTireCompoundIndexOffset],
            data[offset + LmuApiLayoutV1.TelemetryRearTireCompoundIndexOffset],
            ReadVector3(data, offset + LmuApiLayoutV1.TelemetryLocalAccelerationOffset))
        {
            ElapsedTime = ReadDouble(
                data,
                offset + LmuApiLayoutV1.TelemetryElapsedTimeOffset),
            VisualSteeringWheelRangeDegrees = ReadSingle(
                data,
                offset + LmuApiLayoutV1.TelemetryVisualSteeringWheelRangeOffset),
            PhysicalSteeringWheelRangeDegrees = ReadSingle(
                data,
                offset + LmuApiLayoutV1.TelemetryPhysicalSteeringWheelRangeOffset),
        };
    }
}
