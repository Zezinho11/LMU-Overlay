using LmuOverlay.Domain;

namespace LmuOverlay.Widgets;

public static partial class EssentialWidgetStateFactory
{
    public static RelativeWidgetState CreateRelative(
        LmuTelemetrySnapshot snapshot,
        int carsEachSide = 4)
    {
        var player = snapshot.Standings.FirstOrDefault(item => item.IsPlayer) ??
            snapshot.Standings.FirstOrDefault(item =>
                item.VehicleId == snapshot.Player?.VehicleId);
        var playerTelemetry = snapshot.Player;
        var lapLength = snapshot.Session?.LapLengthMeters ?? 0;
        if (player is null || !double.IsFinite(lapLength) || lapLength <= 100)
        {
            return new RelativeWidgetState(Array.Empty<RelativeRowState>());
        }

        var referenceLapSeconds = new[]
            {
                player.LastLapTimeSeconds,
                player.BestLapTimeSeconds,
            }
            .FirstOrDefault(value => double.IsFinite(value) && value is > 20 and < 1_800);
        if (referenceLapSeconds <= 0)
        {
            var lapTimes = snapshot.Standings
                .SelectMany(item => new[] { item.LastLapTimeSeconds, item.BestLapTimeSeconds })
                .Where(value => double.IsFinite(value) && value is > 20 and < 1_800)
                .OrderBy(value => value)
                .ToArray();
            referenceLapSeconds = lapTimes.Length == 0
                ? 120
                : lapTimes[lapTimes.Length / 2];
        }

        var metersPerSecond = lapLength / referenceLapSeconds;
        var relative = snapshot.Standings
            .Where(item => item.IsPlayer || !item.IsInGarage)
            .Select(item => new
            {
                Standing = item,
                DistanceMeters = CircularDistance(
                    item.LapDistanceMeters - player.LapDistanceMeters,
                    lapLength),
            })
            .ToArray();
        var nearestAheadId = relative
            .Where(item => item.DistanceMeters > 0)
            .OrderBy(item => item.DistanceMeters)
            .Select(item => item.Standing.VehicleId)
            .FirstOrDefault(int.MinValue);
        var nearestBehindId = relative
            .Where(item => item.DistanceMeters < 0)
            .OrderByDescending(item => item.DistanceMeters)
            .Select(item => item.Standing.VehicleId)
            .FirstOrDefault(int.MinValue);
        var count = Math.Max(1, carsEachSide);
        var selected = relative
            .Where(item => item.DistanceMeters > 0)
            .OrderBy(item => item.DistanceMeters)
            .Take(count)
            .Concat(relative.Where(item => item.Standing.VehicleId == player.VehicleId))
            .Concat(relative
                .Where(item => item.DistanceMeters < 0)
                .OrderByDescending(item => item.DistanceMeters)
                .Take(count))
            .OrderByDescending(item => item.DistanceMeters)
            .ToArray();
        var rows = selected
            .Select(entry =>
            {
                var item = entry.Standing;
                var isPlayer = item.VehicleId == player.VehicleId;
                var officialAhead = item.VehicleId == nearestAheadId &&
                    IsOfficialGap(playerTelemetry?.GapToCarAheadSeconds ?? 0);
                var officialBehind = item.VehicleId == nearestBehindId &&
                    IsOfficialGap(playerTelemetry?.GapToCarBehindSeconds ?? 0);
                var gap = isPlayer
                    ? 0
                    : officialAhead
                        ? -playerTelemetry!.GapToCarAheadSeconds
                        : officialBehind
                            ? playerTelemetry!.GapToCarBehindSeconds
                            : -entry.DistanceMeters / metersPerSecond;
                return new RelativeRowState(
                item.Position,
                item.DriverName,
                AbbreviateRelativeDriverName(item.DriverName),
                item.VehicleClass,
                AbbreviateVehicleClass(item.VehicleClass),
                ExtractCarNumber(item.VehicleName, item.VehicleModel),
                gap,
                0,
                isPlayer,
                item.IsInPits || item.PitState is not LmuPitState.None)
                {
                    GapSource = isPlayer
                        ? RelativeGapSource.Player
                        : officialAhead
                            ? RelativeGapSource.OfficialAhead
                            : officialBehind
                                ? RelativeGapSource.OfficialBehind
                                : RelativeGapSource.DistanceEstimate,
                    GapConfidence = isPlayer || officialAhead || officialBehind ? 1 : 0.6,
                };
            })
            .ToArray();
        return new RelativeWidgetState(rows);
    }

    private static bool IsOfficialGap(double seconds) =>
        double.IsFinite(seconds) && seconds is > 0 and < 1_800;

    private static double CircularDistance(double distanceMeters, double lapLength)
    {
        var halfLap = lapLength / 2;
        if (distanceMeters > halfLap)
        {
            return distanceMeters - lapLength;
        }

        if (distanceMeters < -halfLap)
        {
            return distanceMeters + lapLength;
        }

        return distanceMeters;
    }

    private static string AbbreviateRelativeDriverName(string driverName)
    {
        var parts = driverName
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "---",
            1 => parts[0],
            _ => $"{char.ToUpperInvariant(parts[0][0])} {parts[^1]}",
        };
    }

    private static string AbbreviateVehicleClass(string vehicleClass)
    {
        var value = vehicleClass.Trim().ToUpperInvariant();
        return value switch
        {
            _ when value.Contains("GT3", StringComparison.Ordinal) ||
                       value.Contains("LMGT", StringComparison.Ordinal) => "GT3",
            _ when value.Contains("HYPER", StringComparison.Ordinal) => "HYP",
            _ when value.Contains("LMP2", StringComparison.Ordinal) => "P2",
            _ => new string(value.Where(char.IsLetterOrDigit).Take(3).ToArray())
                .PadRight(3, '-'),
        };
    }

    private static string FormatTireCompound(LmuPlayerTelemetry player)
    {
        var front = player.FrontTireCompound.Trim();
        var rear = player.RearTireCompound.Trim();
        if (front.Length == 0 && rear.Length == 0)
        {
            return "UNKNOWN";
        }

        return string.Equals(front, rear, StringComparison.OrdinalIgnoreCase) ||
               rear.Length == 0
            ? front
            : front.Length == 0
                ? rear
                : $"{front} / {rear}";
    }

    private static string FormatStandingTireCompound(LmuVehicleStanding standing)
    {
        var front = standing.FrontTireCompound.Trim();
        var rear = standing.RearTireCompound.Trim();
        if (front.Length == 0 && rear.Length == 0)
        {
            return string.Empty;
        }

        return string.Equals(front, rear, StringComparison.OrdinalIgnoreCase) ||
               rear.Length == 0
            ? front
            : front.Length == 0
                ? rear
                : $"{front}/{rear}";
    }

    private static DashboardSectorTimes CreateSectorTimes(
        LmuVehicleStanding? standing,
        double currentLapSeconds)
    {
        if (standing is null)
        {
            return default;
        }

        var currentSector2 = PositiveDifference(
            standing.CurrentSector2CumulativeSeconds,
            standing.CurrentSector1Seconds);
        var currentSector3 = standing.Sector == 0
            ? PositiveDifference(currentLapSeconds, standing.CurrentSector2CumulativeSeconds)
            : 0;
        return new(
            standing.CurrentSector1Seconds,
            currentSector2,
            currentSector3,
            standing.LastSector1Seconds,
            PositiveDifference(
                standing.LastSector2CumulativeSeconds,
                standing.LastSector1Seconds),
            PositiveDifference(
                standing.LastLapTimeSeconds,
                standing.LastSector2CumulativeSeconds),
            standing.BestSector1Seconds,
            PositiveDifference(
                standing.BestSector2CumulativeSeconds,
                standing.BestSector1Seconds),
            PositiveDifference(
                standing.BestLapTimeSeconds,
                standing.BestSector2CumulativeSeconds));
    }

    private static double PositiveDifference(double total, double previous) =>
        total > previous && previous >= 0 ? total - previous : 0;
}
