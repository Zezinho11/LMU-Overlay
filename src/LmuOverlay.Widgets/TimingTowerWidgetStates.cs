namespace LmuOverlay.Widgets;

public sealed record LiveStandingsWidgetState(string PlayerClass,
    IReadOnlyList<LiveStandingsClassState> Classes, string SessionName = "",
    double SessionRemainingSeconds = 0, bool IsQualifying = false);

public sealed record LiveStandingsClassState(string ClassName, bool IsPlayerClass,
    IReadOnlyList<LiveStandingsRowState> Rows);

public sealed record LiveStandingsRowState(
    int ClassPosition, string DriverName, string DriverAbbreviation, string VehicleName,
    string VehicleModel, string CarNumber, int CompletedLaps, double GapToLeaderSeconds,
    double IntervalSeconds, int IntervalLaps, double LastLapTimeSeconds,
    double BestLapTimeSeconds, bool IsPlayer, bool IsInPitLane,
    bool IsQualifying = false, double VirtualEnergyFraction = -1,
    string TireCompound = "", int TireCompoundIndex = 0);

public sealed record RelativeWidgetState(IReadOnlyList<RelativeRowState> Rows,
    string SessionName = "", double SessionRemainingSeconds = 0);

public sealed record RelativeRowState(
    int OverallPosition, string DriverName, string DriverDisplayName, string VehicleClass,
    string ClassAbbreviation, string CarNumber, double RelativeGapSeconds,
    int RelativeLaps, bool IsPlayer, bool IsInPitLane)
{
    public RelativeGapSource GapSource { get; init; } = RelativeGapSource.DistanceEstimate;
    public double GapConfidence { get; init; } = 0.6;
}

public enum RelativeGapSource
{
    Unavailable, Player, OfficialAhead, OfficialBehind, DistanceEstimate,
}
