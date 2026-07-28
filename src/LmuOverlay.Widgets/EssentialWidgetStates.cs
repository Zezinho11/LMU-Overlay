using LmuOverlay.Domain;

namespace LmuOverlay.Widgets;

public sealed record DashboardWidgetState(
    bool Available,
    double SpeedKilometersPerHour,
    string Gear,
    double EngineRpm,
    double EngineRpmFraction,
    double FuelLiters,
    int Position,
    int LapNumber,
    string TrackName,
    double DeltaBestSeconds,
    LmuWheelTemperatures TireTemperatures);

public sealed record InputsWidgetState(
    bool Available,
    double Throttle,
    double Brake,
    double Clutch,
    double Steering,
    bool AbsActive,
    bool TractionControlActive);

public sealed record LiveStandingsWidgetState(
    string PlayerClass,
    IReadOnlyList<LiveStandingsClassState> Classes);

public sealed record LiveStandingsClassState(
    string ClassName,
    bool IsPlayerClass,
    IReadOnlyList<LiveStandingsRowState> Rows);

public sealed record LiveStandingsRowState(
    int ClassPosition,
    string DriverName,
    string VehicleName,
    int CompletedLaps,
    double GapToLeaderSeconds,
    double LastLapTimeSeconds,
    double BestLapTimeSeconds,
    bool IsPlayer,
    bool IsInPitLane);

public static class EssentialWidgetStateFactory
{
    public static DashboardWidgetState CreateDashboard(LmuTelemetrySnapshot snapshot)
    {
        if (snapshot.Player is not { } player)
        {
            return new(
                false, 0, "N", 0, 0, 0, 0, 0,
                snapshot.Session?.TrackName ?? string.Empty,
                0,
                new LmuWheelTemperatures(0, 0, 0, 0));
        }

        return new(
            true,
            player.SpeedKilometersPerHour,
            FormatGear(player.Gear),
            player.EngineRpm,
            player.EngineMaximumRpm > 0
                ? Math.Clamp(player.EngineRpm / player.EngineMaximumRpm, 0, 1)
                : 0,
            player.FuelLiters,
            player.Position,
            player.LapNumber,
            snapshot.Session?.TrackName ?? string.Empty,
            player.DeltaBestSeconds,
            player.TireTemperatures);
    }

    public static InputsWidgetState CreateInputs(LmuTelemetrySnapshot snapshot)
    {
        if (snapshot.Player is not { } player)
        {
            return new(false, 0, 0, 0, 0, false, false);
        }

        return new(
            true,
            ClampInput(player.Throttle),
            ClampInput(player.Brake),
            ClampInput(player.Clutch),
            Math.Clamp(player.Steering, -1, 1),
            player.AbsActive,
            player.TractionControlActive);
    }

    public static LiveStandingsWidgetState CreateLiveStandings(
        LmuTelemetrySnapshot snapshot)
    {
        var ordered = snapshot.Standings
            .OrderBy(item => item.Position)
            .ToArray();
        var playerClass = ordered.FirstOrDefault(item => item.IsPlayer)?.VehicleClass
            ?? string.Empty;

        var classes = ordered
            .GroupBy(item => string.IsNullOrWhiteSpace(item.VehicleClass)
                ? "Unknown"
                : item.VehicleClass)
            .Select(group =>
            {
                var isPlayerClass = string.Equals(
                    group.Key,
                    playerClass,
                    StringComparison.OrdinalIgnoreCase);
                var classOrder = group.OrderBy(item => item.Position).ToArray();
                var visible = isPlayerClass ? classOrder : classOrder.Take(1);
                var rows = visible.Select((item, index) => new LiveStandingsRowState(
                    index + 1,
                    item.DriverName,
                    item.VehicleName,
                    item.CompletedLaps,
                    item.GapToLeaderSeconds,
                    item.LastLapTimeSeconds,
                    item.BestLapTimeSeconds,
                    item.IsPlayer,
                    item.IsInPits || item.PitState is not LmuPitState.None))
                    .ToArray();
                return new LiveStandingsClassState(
                    group.Key,
                    isPlayerClass,
                    rows);
            })
            .OrderByDescending(item => item.IsPlayerClass)
            .ThenBy(item => item.ClassName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new LiveStandingsWidgetState(playerClass, classes);
    }

    private static string FormatGear(int gear) => gear switch
    {
        < 0 => "R",
        0 => "N",
        _ => gear.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    private static double ClampInput(double value) => Math.Clamp(value, 0, 1);
}
