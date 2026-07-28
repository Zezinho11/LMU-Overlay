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

public sealed record RelativeWidgetState(
    IReadOnlyList<RelativeRowState> Rows);

public sealed record RelativeRowState(
    int OverallPosition,
    string DriverName,
    string VehicleClass,
    double RelativeGapSeconds,
    int RelativeLaps,
    bool IsPlayer,
    bool IsInPitLane);

public sealed record SessionFlagsWidgetState(
    bool Available,
    string SessionName,
    string PhaseName,
    string FlagName,
    double RemainingSeconds,
    int CurrentLap,
    int MaximumLaps,
    double AmbientTemperatureCelsius,
    double TrackTemperatureCelsius);

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

    public static RelativeWidgetState CreateRelative(
        LmuTelemetrySnapshot snapshot,
        int carsEachSide = 3)
    {
        var ordered = snapshot.Standings.OrderBy(item => item.Position).ToArray();
        var playerIndex = Array.FindIndex(ordered, item => item.IsPlayer);
        if (playerIndex < 0)
        {
            return new RelativeWidgetState(Array.Empty<RelativeRowState>());
        }

        var player = ordered[playerIndex];
        var start = Math.Max(0, playerIndex - Math.Max(1, carsEachSide));
        var end = Math.Min(ordered.Length - 1, playerIndex + Math.Max(1, carsEachSide));
        var rows = ordered[start..(end + 1)]
            .Select(item => new RelativeRowState(
                item.Position,
                item.DriverName,
                item.VehicleClass,
                item.IsPlayer ? 0 : item.GapToLeaderSeconds - player.GapToLeaderSeconds,
                player.CompletedLaps - item.CompletedLaps,
                item.IsPlayer,
                item.IsInPits || item.PitState is not LmuPitState.None))
            .ToArray();
        return new RelativeWidgetState(rows);
    }

    public static SessionFlagsWidgetState CreateSessionFlags(
        LmuTelemetrySnapshot snapshot)
    {
        if (snapshot.Session is not { } session)
        {
            return new(false, string.Empty, string.Empty, "NO DATA", 0, 0, 0, 0, 0);
        }

        var playerStanding = snapshot.Standings.FirstOrDefault(item => item.IsPlayer);
        var globalYellow = session.GamePhase == LmuGamePhase.FullCourseYellow ||
            snapshot.Standings.Any(item => item.IsUnderYellow);
        var flagName = globalYellow
            ? "YELLOW"
            : session.GamePhase == LmuGamePhase.GreenFlag
                ? "GREEN"
                : session.GamePhase.ToString().ToUpperInvariant();

        return new(
            true,
            FormatSessionKind(session.Kind),
            FormatGamePhase(session.GamePhase),
            flagName,
            session.EndElapsedTime > 0
                ? Math.Max(0, session.EndElapsedTime - session.CurrentElapsedTime)
                : 0,
            playerStanding?.CompletedLaps ?? snapshot.Player?.LapNumber ?? 0,
            session.MaximumLaps,
            session.Weather.AmbientTemperatureCelsius,
            session.Weather.TrackTemperatureCelsius);
    }

    private static string FormatGear(int gear) => gear switch
    {
        < 0 => "R",
        0 => "N",
        _ => gear.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    private static string FormatSessionKind(LmuSessionKind kind) => kind switch
    {
        LmuSessionKind.TestDay => "TEST DAY",
        LmuSessionKind.Qualifying => "QUALIFYING",
        _ => kind.ToString().ToUpperInvariant(),
    };

    private static string FormatGamePhase(LmuGamePhase phase) => phase switch
    {
        LmuGamePhase.BeforeSession => "BEFORE SESSION",
        LmuGamePhase.GridWalk => "GRID WALK",
        LmuGamePhase.FormationLap => "FORMATION LAP",
        LmuGamePhase.StartingLights => "STARTING LIGHTS",
        LmuGamePhase.GreenFlag => "GREEN FLAG",
        LmuGamePhase.FullCourseYellow => "FULL COURSE YELLOW",
        LmuGamePhase.SessionOver => "SESSION OVER",
        _ => phase.ToString().ToUpperInvariant(),
    };

    private static double ClampInput(double value) => Math.Clamp(value, 0, 1);
}
