using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using LmuOverlay.Domain;

namespace LmuOverlay.LmuSharedMemory;

public static class LmuSnapshotParser
{
    public static LmuProbeSnapshot Parse(ReadOnlySpan<byte> data)
    {
        var snapshot = ParseTelemetry(data);
        return new(
            snapshot.State,
            snapshot.GameVersion == 0
                ? string.Empty
                : snapshot.GameVersion.ToString(CultureInfo.InvariantCulture),
            snapshot.Session?.TrackName ?? string.Empty,
            snapshot.Session?.SessionCode ?? 0,
            snapshot.Player?.VehicleName ?? string.Empty,
            snapshot.ActiveVehicles,
            snapshot.ScoredVehicles,
            snapshot.Player is not null,
            snapshot.CapturedAt,
            snapshot.Detail);
    }

    public static LmuTelemetrySnapshot ParseTelemetry(ReadOnlySpan<byte> data)
    {
        if (data.Length < LmuApiLayoutV1.ObjectSize)
        {
            return LmuTelemetrySnapshot.Unavailable(
                LmuConnectionState.IncompatibleLayout,
                $"Expected at least {LmuApiLayoutV1.ObjectSize} bytes, received {data.Length}.");
        }

        var activeVehicles = data[LmuApiLayoutV1.ActiveVehiclesOffset];
        var scoredVehicles = ReadInt32(data, LmuApiLayoutV1.ScoredVehiclesOffset);
        var playerVehicleIndex = data[LmuApiLayoutV1.PlayerVehicleIndexOffset];
        var hasPlayerVehicle = ReadBoolean(data, LmuApiLayoutV1.PlayerHasVehicleOffset);

        if (activeVehicles > LmuApiLayoutV1.MaximumVehicles ||
            scoredVehicles is < 0 or > LmuApiLayoutV1.MaximumVehicles)
        {
            return LmuTelemetrySnapshot.Unavailable(
                LmuConnectionState.InvalidData,
                "Vehicle counts are outside the supported LMU layout range.") with
            {
                GameVersion = ReadInt32(data, LmuApiLayoutV1.GameVersionOffset),
                ActiveVehicles = activeVehicles,
                ScoredVehicles = scoredVehicles
            };
        }

        var vehicleModels = ParseVehicleModels(data, activeVehicles);
        var standings = ParseStandings(data, scoredVehicles, vehicleModels);
        var session = ParseSession(data);
        var playerStanding = standings.FirstOrDefault(vehicle => vehicle.IsPlayer);
        var player = hasPlayerVehicle && playerVehicleIndex < activeVehicles
            ? ParsePlayer(data, playerVehicleIndex, playerStanding)
            : null;

        return new(
            LmuConnectionState.Connected,
            ReadInt32(data, LmuApiLayoutV1.GameVersionOffset),
            ReadUInt32(data, LmuApiLayoutV1.EventOffset(LmuApiLayoutV1.ScoringUpdateEventIndex)),
            ReadUInt32(data, LmuApiLayoutV1.EventOffset(LmuApiLayoutV1.TelemetryUpdateEventIndex)),
            activeVehicles,
            scoredVehicles,
            session,
            player,
            standings,
            DateTimeOffset.UtcNow,
            "Read-only LMU shared-memory snapshot parsed successfully.");
    }

    private static LmuSessionSnapshot ParseSession(ReadOnlySpan<byte> data)
    {
        var sessionCode = ReadInt32(data, LmuApiLayoutV1.SessionCodeOffset);
        return new(
            ReadText(data, LmuApiLayoutV1.TrackNameOffset, LmuApiLayoutV1.TrackNameLength),
            sessionCode,
            ToSessionKind(sessionCode),
            (LmuGamePhase)data[LmuApiLayoutV1.GamePhaseOffset],
            ReadDouble(data, LmuApiLayoutV1.SessionCurrentElapsedTimeOffset),
            ReadDouble(data, LmuApiLayoutV1.SessionEndElapsedTimeOffset),
            LmuSessionLimits.NormalizeMaximumLaps(
                ReadInt32(data, LmuApiLayoutV1.SessionMaximumLapsOffset)),
            ReadDouble(data, LmuApiLayoutV1.SessionLapLengthOffset),
            ReadBoolean(data, LmuApiLayoutV1.IsRealtimeOffset),
            ReadText(data, LmuApiLayoutV1.PlayerNameOffset, LmuApiLayoutV1.PlayerNameLength),
            new(
                ReadDouble(data, LmuApiLayoutV1.CloudinessOffset),
                ReadDouble(data, LmuApiLayoutV1.RainIntensityOffset),
                ReadDouble(data, LmuApiLayoutV1.AmbientTemperatureOffset),
                ReadDouble(data, LmuApiLayoutV1.TrackTemperatureOffset),
                ReadVector3(data, LmuApiLayoutV1.WindOffset),
                ReadDouble(data, LmuApiLayoutV1.MinimumPathWetnessOffset),
                ReadDouble(data, LmuApiLayoutV1.MaximumPathWetnessOffset),
                ReadDouble(data, LmuApiLayoutV1.AveragePathWetnessOffset),
                data[LmuApiLayoutV1.TrackGripLevelOffset]));
    }

    private static LmuVehicleStanding[] ParseStandings(
        ReadOnlySpan<byte> data,
        int vehicleCount,
        IReadOnlyDictionary<int, string> vehicleModels)
    {
        var standings = new LmuVehicleStanding[vehicleCount];
        for (var index = 0; index < vehicleCount; index++)
        {
            var offset = LmuApiLayoutV1.VehicleScoringOffset(index);
            standings[index] = new(
                ReadInt32(data, offset + LmuApiLayoutV1.ScoringVehicleIdOffset),
                ReadText(
                    data,
                    offset + LmuApiLayoutV1.ScoringDriverNameOffset,
                    LmuApiLayoutV1.ScoringDriverNameLength),
                ReadText(
                    data,
                    offset + LmuApiLayoutV1.ScoringVehicleNameOffset,
                    LmuApiLayoutV1.ScoringVehicleNameLength),
                vehicleModels.GetValueOrDefault(
                    ReadInt32(data, offset + LmuApiLayoutV1.ScoringVehicleIdOffset),
                    string.Empty),
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
                ReadDouble(data, offset + LmuApiLayoutV1.ScoringCurrentSector2Offset));
        }

        return standings;
    }

    private static IReadOnlyDictionary<int, string> ParseVehicleModels(
        ReadOnlySpan<byte> data,
        int vehicleCount)
    {
        var models = new Dictionary<int, string>(vehicleCount);
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
            models[vehicleId] = string.Join(
                " ",
                new[] { vehicleName, vehicleModel }
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase));
        }

        return models;
    }

    private static LmuPlayerTelemetry ParsePlayer(
        ReadOnlySpan<byte> data,
        int playerVehicleIndex,
        LmuVehicleStanding? standing)
    {
        var offset = LmuApiLayoutV1.VehicleTelemetryOffset(playerVehicleIndex);
        var velocity = ReadVector3(
            data,
            offset + LmuApiLayoutV1.TelemetryLocalVelocityOffset);

        return new(
            ReadInt32(data, offset + LmuApiLayoutV1.TelemetryVehicleIdOffset),
            ReadText(
                data,
                offset + LmuApiLayoutV1.VehicleNameOffset,
                LmuApiLayoutV1.VehicleNameLength),
            ReadText(
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
            ParseDamage(data, offset),
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
            ReadVector3(data, offset + LmuApiLayoutV1.TelemetryLocalAccelerationOffset));
    }

    private static LmuDamageSnapshot ParseDamage(
        ReadOnlySpan<byte> data,
        int telemetryOffset)
    {
        var maximumDentSeverity = 0;
        var damagedAreas = 0;
        for (var index = 0; index < LmuApiLayoutV1.TelemetryDentSeverityCount; index++)
        {
            var severity = data[
                telemetryOffset +
                LmuApiLayoutV1.TelemetryDentSeverityOffset +
                index];
            maximumDentSeverity = Math.Max(maximumDentSeverity, severity);
            if (severity > 0)
            {
                damagedAreas++;
            }
        }

        return new(
            data[telemetryOffset + LmuApiLayoutV1.TelemetryScheduledStopsOffset],
            ReadBoolean(data, telemetryOffset + LmuApiLayoutV1.TelemetryOverheatingOffset),
            ReadBoolean(data, telemetryOffset + LmuApiLayoutV1.TelemetryBodyDetachedOffset),
            maximumDentSeverity,
            damagedAreas,
            ReadDouble(
                data,
                telemetryOffset + LmuApiLayoutV1.TelemetryLastImpactElapsedTimeOffset),
            ReadDouble(
                data,
                telemetryOffset + LmuApiLayoutV1.TelemetryLastImpactMagnitudeOffset),
            ReadWheelCondition(data, telemetryOffset, 0),
            ReadWheelCondition(data, telemetryOffset, 1),
            ReadWheelCondition(data, telemetryOffset, 2),
            ReadWheelCondition(data, telemetryOffset, 3));
    }

    private static LmuWheelCondition ReadWheelCondition(
        ReadOnlySpan<byte> data,
        int telemetryOffset,
        int wheelIndex)
    {
        var wheelOffset = telemetryOffset +
            LmuApiLayoutV1.TelemetryWheelArrayOffset +
            (wheelIndex * LmuApiLayoutV1.TelemetryWheelSize);
        return new(
            ReadBoolean(data, wheelOffset + LmuApiLayoutV1.TelemetryWheelFlatOffset),
            ReadBoolean(data, wheelOffset + LmuApiLayoutV1.TelemetryWheelDetachedOffset));
    }

    private static double ReadWheelTemperature(
        ReadOnlySpan<byte> data,
        int telemetryOffset,
        int wheelIndex) =>
        ReadDouble(
            data,
            telemetryOffset +
            LmuApiLayoutV1.TelemetryWheelArrayOffset +
            (wheelIndex * LmuApiLayoutV1.TelemetryWheelSize) +
            LmuApiLayoutV1.TelemetryWheelCarcassTemperatureOffset) - 273.15;

    private static double ReadWheelWear(
        ReadOnlySpan<byte> data,
        int telemetryOffset,
        int wheelIndex)
    {
        var value = ReadDouble(
            data,
            telemetryOffset +
            LmuApiLayoutV1.TelemetryWheelArrayOffset +
            (wheelIndex * LmuApiLayoutV1.TelemetryWheelSize) +
            LmuApiLayoutV1.TelemetryWheelWearOffset);
        return double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;
    }

    private static LmuSessionKind ToSessionKind(int sessionCode) =>
        sessionCode switch
        {
            0 => LmuSessionKind.TestDay,
            >= 1 and <= 4 => LmuSessionKind.Practice,
            >= 5 and <= 8 => LmuSessionKind.Qualifying,
            9 => LmuSessionKind.Warmup,
            >= 10 and <= 13 => LmuSessionKind.Race,
            _ => LmuSessionKind.Unknown
        };

    private static bool ReadBoolean(ReadOnlySpan<byte> data, int offset) =>
        data[offset] != 0;

    private static short ReadInt16(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadInt16LittleEndian(data.Slice(offset, sizeof(short)));

    private static int ReadInt32(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, sizeof(int)));

    private static uint ReadUInt32(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, sizeof(uint)));

    private static float ReadSingle(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadSingleLittleEndian(data.Slice(offset, sizeof(float)));

    private static double ReadDouble(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadDoubleLittleEndian(data.Slice(offset, sizeof(double)));

    private static LmuVector3 ReadVector3(ReadOnlySpan<byte> data, int offset) =>
        new(
            ReadDouble(data, offset),
            ReadDouble(data, offset + sizeof(double)),
            ReadDouble(data, offset + (2 * sizeof(double))));

    private static string ReadText(ReadOnlySpan<byte> data, int offset, int length)
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
