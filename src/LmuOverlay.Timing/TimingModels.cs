namespace LmuOverlay.Widgets;

public readonly record struct DashboardSectorTimes(
    double CurrentSector1Seconds,
    double CurrentSector2Seconds,
    double CurrentSector3Seconds,
    double LastSector1Seconds,
    double LastSector2Seconds,
    double LastSector3Seconds,
    double BestSector1Seconds,
    double BestSector2Seconds,
    double BestSector3Seconds,
    SectorReferenceOrigin Sector1ReferenceOrigin = SectorReferenceOrigin.None,
    SectorReferenceOrigin Sector2ReferenceOrigin = SectorReferenceOrigin.None,
    SectorReferenceOrigin Sector3ReferenceOrigin = SectorReferenceOrigin.None,
    int RecentSectorIndex = -1,
    double RecentSectorTimeSeconds = 0,
    double RecentSectorReferenceSeconds = 0,
    long RecentSectorExpiresAtTimestamp = 0)
{
    public double OptimalLapTimeSeconds =>
        IsValid(BestSector1Seconds) &&
        IsValid(BestSector2Seconds) &&
        IsValid(BestSector3Seconds)
            ? BestSector1Seconds + BestSector2Seconds + BestSector3Seconds
            : 0;

    private static bool IsValid(double seconds) =>
        seconds > 0 && double.IsFinite(seconds);
}

public enum SectorReferenceOrigin
{
    None,
    OutLap,
    Session,
    Saved,
}

public readonly record struct SectorReferenceSeed(
    double Sector1Seconds,
    double Sector2Seconds,
    double Sector3Seconds)
{
    public double Optimal =>
        Sector1Seconds > 0 && Sector2Seconds > 0 && Sector3Seconds > 0
            ? Sector1Seconds + Sector2Seconds + Sector3Seconds
            : 0;

    public double this[int index] => index switch
    {
        0 => Sector1Seconds,
        1 => Sector2Seconds,
        2 => Sector3Seconds,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
}

public readonly record struct PersonalBestLap(
    double LapTimeSeconds,
    double Sector1Seconds,
    double Sector2Seconds,
    double Sector3Seconds)
{
    public bool IsValid =>
        IsPlausible(LapTimeSeconds) &&
        IsPlausible(Sector1Seconds) &&
        IsPlausible(Sector2Seconds) &&
        IsPlausible(Sector3Seconds) &&
        Math.Abs(
            LapTimeSeconds -
            (Sector1Seconds + Sector2Seconds + Sector3Seconds)) < 0.05;

    public SectorReferenceSeed Sectors => new(
        Sector1Seconds,
        Sector2Seconds,
        Sector3Seconds);

    private static bool IsPlausible(double seconds) =>
        double.IsFinite(seconds) && seconds is > 1 and < 1_800;
}
