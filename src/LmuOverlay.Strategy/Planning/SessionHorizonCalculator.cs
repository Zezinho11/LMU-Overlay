using LmuOverlay.Domain;

namespace LmuOverlay.Strategy.Planning;

public static class SessionHorizonCalculator
{
    public static int RemainingLaps(
        LmuSessionSnapshot session,
        LmuVehicleStanding? playerStanding,
        int completedLaps)
    {
        if (LmuSessionLimits.HasFiniteLapLimit(session.MaximumLaps))
        {
            return Math.Max(0, session.MaximumLaps - completedLaps);
        }

        var remainingSeconds = Math.Max(
            0,
            session.EndElapsedTime - session.CurrentElapsedTime);
        var referenceLapSeconds = ReferenceLapSeconds(playerStanding);
        return remainingSeconds > 0 && referenceLapSeconds > 0
            ? (int)Math.Ceiling(remainingSeconds / referenceLapSeconds)
            : 0;
    }

    public static double ReferenceLapSeconds(LmuVehicleStanding? standing) =>
        standing?.LastLapTimeSeconds > 0
            ? standing.LastLapTimeSeconds
            : standing?.BestLapTimeSeconds > 0
                ? standing.BestLapTimeSeconds
                : 0;
}
