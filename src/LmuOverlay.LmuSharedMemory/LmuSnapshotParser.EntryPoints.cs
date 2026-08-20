using System.Globalization;
using LmuOverlay.Domain;

namespace LmuOverlay.LmuSharedMemory;

public static partial class LmuSnapshotParser
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

        var vehicleMetadata = ParseVehicleMetadata(data, activeVehicles);
        var standings = ParseStandings(data, scoredVehicles, vehicleMetadata);
        var session = ParseSession(data);
        var playerStanding = standings.FirstOrDefault(vehicle => vehicle.IsPlayer)
            ?? standings.FirstOrDefault(vehicle =>
                !string.IsNullOrWhiteSpace(session.PlayerName) &&
                string.Equals(
                    vehicle.DriverName,
                    session.PlayerName,
                    StringComparison.OrdinalIgnoreCase));
        var resolvedPlayerIndex = ResolvePlayerVehicleIndex(
            data,
            activeVehicles,
            playerVehicleIndex,
            hasPlayerVehicle,
            playerStanding?.VehicleId);
        var player = resolvedPlayerIndex >= 0
            ? ParsePlayer(data, resolvedPlayerIndex, playerStanding)
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

    public static LmuTelemetrySnapshot ParseTelemetryUpdate(
        ReadOnlySpan<byte> data,
        LmuTelemetrySnapshot previous)
    {
        if (data.Length < LmuApiLayoutV1.ObjectSize ||
            previous.State != LmuConnectionState.Connected ||
            previous.Player is null)
        {
            return ParseTelemetry(data);
        }

        var scoringSequence = ReadUInt32(
            data,
            LmuApiLayoutV1.EventOffset(LmuApiLayoutV1.ScoringUpdateEventIndex));
        if (scoringSequence != previous.ScoringSequence)
        {
            return ParseTelemetry(data);
        }

        var activeVehicles = data[LmuApiLayoutV1.ActiveVehiclesOffset];
        var playerVehicleIndex = data[LmuApiLayoutV1.PlayerVehicleIndexOffset];
        var hasPlayerVehicle = ReadBoolean(
            data,
            LmuApiLayoutV1.PlayerHasVehicleOffset);
        var resolvedPlayerIndex = ResolvePlayerVehicleIndex(
            data,
            activeVehicles,
            playerVehicleIndex,
            hasPlayerVehicle,
            previous.Player.VehicleId);
        if (resolvedPlayerIndex < 0)
        {
            return ParseTelemetry(data);
        }

        var standing = previous.Standings.FirstOrDefault(
            item => item.VehicleId == previous.Player.VehicleId);
        return previous with
        {
            TelemetrySequence = ReadUInt32(
                data,
                LmuApiLayoutV1.EventOffset(LmuApiLayoutV1.TelemetryUpdateEventIndex)),
            Player = ParsePlayer(
                data,
                resolvedPlayerIndex,
                standing,
                previous.Player),
            CapturedAt = DateTimeOffset.UtcNow,
        };
    }

    public static LmuTelemetrySnapshot ParsePlayerTelemetryBlock(
        ReadOnlySpan<byte> vehicleData,
        LmuTelemetrySnapshot previous,
        uint telemetrySequence)
    {
        if (vehicleData.Length < LmuApiLayoutV1.VehicleTelemetrySize ||
            previous.State != LmuConnectionState.Connected ||
            previous.Player is not { } previousPlayer)
        {
            return previous;
        }

        if (ReadInt32(vehicleData, LmuApiLayoutV1.TelemetryVehicleIdOffset) !=
            previousPlayer.VehicleId)
        {
            return previous;
        }

        var standing = previous.Standings.FirstOrDefault(
            item => item.VehicleId == previousPlayer.VehicleId);
        var player = ParsePlayerAtOffset(
            vehicleData,
            0,
            standing,
            previousPlayer);
        if (player == previousPlayer && telemetrySequence == previous.TelemetrySequence)
        {
            return previous;
        }

        return previous with
        {
            TelemetrySequence = telemetrySequence,
            Player = player,
            CapturedAt = DateTimeOffset.UtcNow,
        };
    }

    public static double ReadPlayerElapsedTime(ReadOnlySpan<byte> vehicleData) =>
        vehicleData.Length >= LmuApiLayoutV1.TelemetryElapsedTimeOffset + sizeof(double)
            ? ReadDouble(vehicleData, LmuApiLayoutV1.TelemetryElapsedTimeOffset)
            : double.NaN;

    private static int ResolvePlayerVehicleIndex(
        ReadOnlySpan<byte> data,
        int activeVehicles,
        int reportedIndex,
        bool hasReportedVehicle,
        int? playerVehicleId)
    {
        if (hasReportedVehicle && reportedIndex < activeVehicles)
        {
            return reportedIndex;
        }

        if (playerVehicleId is not { } vehicleId)
        {
            return -1;
        }

        for (var index = 0; index < activeVehicles; index++)
        {
            var telemetryOffset = LmuApiLayoutV1.VehicleTelemetryOffset(index);
            if (ReadInt32(
                    data,
                    telemetryOffset + LmuApiLayoutV1.TelemetryVehicleIdOffset) == vehicleId)
            {
                return index;
            }
        }

        return -1;
    }
}
