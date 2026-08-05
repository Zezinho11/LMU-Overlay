using LmuOverlay.Domain;

namespace LmuOverlay.Widgets;

public sealed record RaceScenarioContext(
    LmuGamePhase GamePhase,
    double RainIntensity,
    double TrackWetness,
    double RainTrendPerSample,
    double GapAheadSeconds,
    double GapBehindSeconds,
    int CompletedLaps,
    int LapsUntilPit,
    int SuggestedPitLap,
    double CurrentMaximumTireWearFraction,
    double TireWearLimitFraction,
    string TireCompound);

public sealed record RaceScenarioAdvice(
    string FlagState,
    string Weather,
    string Traffic);

public static class RaceScenarioAdvisor
{
    public static RaceScenarioAdvice Calculate(
        EnduranceStrategyInput strategyInput,
        EnduranceStrategyPlan basePlan,
        RaceScenarioContext context)
    {
        if (!basePlan.Available)
        {
            return new(
                "FLAGS · LEARNING BASE PLAN",
                "WEATHER · LEARNING CONDITIONS",
                "TRAFFIC · LEARNING GAPS");
        }

        return new(
            FlagStateAdvice(context),
            WeatherAdvice(context),
            TrafficAdvice(context));
    }

    private static string FlagStateAdvice(RaceScenarioContext context)
    {
        if (context.GamePhase == LmuGamePhase.Stopped)
        {
            return "RED · HOLD STRATEGY / REASSESS AT RESTART";
        }

        if (context.GamePhase == LmuGamePhase.FullCourseYellow)
        {
            return "YELLOW · INCIDENT CAUTION · KEEP GREEN-PACE PIT PLAN";
        }

        return "FLAGS · GREEN · NO PIT-LOSS DISCOUNT";
    }

    private static string WeatherAdvice(RaceScenarioContext context)
    {
        var rain = Math.Clamp(context.RainIntensity, 0, 1);
        var wetness = Math.Clamp(context.TrackWetness, 0, 1);
        var rising = context.RainTrendPerSample > 0.002;
        var falling = context.RainTrendPerSample < -0.002;
        var trend = rising ? "RISING" : falling ? "FALLING" : "STABLE";
        var compound = string.IsNullOrWhiteSpace(context.TireCompound)
            ? "UNKNOWN TYRE"
            : context.TireCompound.ToUpperInvariant();

        if (rain >= 0.5 || wetness >= 0.55)
        {
            return $"HEAVY RAIN {trend} · WET WINDOW · {compound}";
        }

        if (rain >= 0.15 || wetness >= 0.25)
        {
            return $"RAIN {trend} · PREPARE WET SET · PIT ±1 LAP";
        }

        if (rain >= 0.03 || wetness >= 0.08)
        {
            return $"LIGHT RAIN {trend} · EXTEND / AVOID DOUBLE STOP";
        }

        return rising
            ? "DRY · RAIN RISING · PROTECT PIT WINDOW"
            : "DRY STABLE · KEEP GREEN PLAN";
    }

    private static string TrafficAdvice(RaceScenarioContext context)
    {
        var ahead = NormalizeGap(context.GapAheadSeconds);
        var behind = NormalizeGap(context.GapBehindSeconds);
        if (ahead is > 0 and < 1.5)
        {
            var earlyLap = Math.Max(
                context.CompletedLaps + 1,
                context.SuggestedPitLap - 1);
            return $"TRAFFIC +{ahead:0.0}s · UNDERCUT TARGET L{earlyLap}";
        }

        if (behind is > 0 and < 1.5)
        {
            return $"PRESSURE -{behind:0.0}s · EXTEND 1 LAP IF PACE HOLDS";
        }

        if (ahead is > 0 and < 3)
        {
            return $"DIRTY AIR +{ahead:0.0}s · MONITOR UNDERCUT";
        }

        return "CLEAN AIR · KEEP GREEN PIT WINDOW";
    }

    private static double NormalizeGap(double value) =>
        double.IsFinite(value) && value > 0 ? value : 0;
}
