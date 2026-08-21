using LmuOverlay.Domain;

namespace LmuOverlay.Widgets;

public static class TireStrategyCalculator
{
    public static LmuWheelWear CurrentWear(EnduranceStrategyInput input) =>
        Maximum(input.CurrentTireWear) > 0
            ? input.CurrentTireWear
            : Uniform(input.CurrentMaximumTireWearFraction);

    public static LmuWheelWear WearPerLap(EnduranceStrategyInput input) =>
        Maximum(input.TireWearPerLap) > 0
            ? input.TireWearPerLap
            : Uniform(input.TireWearFractionPerLap);

    public static double Maximum(LmuWheelWear wear) => Math.Max(
        Math.Max(wear.FrontLeftFraction, wear.FrontRightFraction),
        Math.Max(wear.RearLeftFraction, wear.RearRightFraction));

    public static bool WouldExceed(
        LmuWheelWear current,
        LmuWheelWear perLap,
        int laps,
        double limit) =>
        current.FrontLeftFraction + perLap.FrontLeftFraction * laps > limit ||
        current.FrontRightFraction + perLap.FrontRightFraction * laps > limit ||
        current.RearLeftFraction + perLap.RearLeftFraction * laps > limit ||
        current.RearRightFraction + perLap.RearRightFraction * laps > limit;

    public static LmuWheelWear AddWear(
        LmuWheelWear current,
        LmuWheelWear perLap,
        int laps) => new(
        current.FrontLeftFraction + perLap.FrontLeftFraction * laps,
        current.FrontRightFraction + perLap.FrontRightFraction * laps,
        current.RearLeftFraction + perLap.RearLeftFraction * laps,
        current.RearRightFraction + perLap.RearRightFraction * laps);

    public static IReadOnlyList<string> RequiredForNextStint(
        LmuWheelWear current,
        LmuWheelWear perLap,
        int laps,
        double limit)
    {
        if (Maximum(perLap) <= 0) return Array.Empty<string>();
        var tires = new List<string>(4);
        if (current.FrontLeftFraction + perLap.FrontLeftFraction * laps > limit) tires.Add("FL");
        if (current.FrontRightFraction + perLap.FrontRightFraction * laps > limit) tires.Add("FR");
        if (current.RearLeftFraction + perLap.RearLeftFraction * laps > limit) tires.Add("RL");
        if (current.RearRightFraction + perLap.RearRightFraction * laps > limit) tires.Add("RR");
        return tires;
    }

    public static IReadOnlyList<string> ServiceForUpcomingStints(
        LmuWheelWear current,
        LmuWheelWear perLap,
        int nextStintLaps,
        int followingStintLaps,
        double limit,
        IReadOnlyList<string>? previousService = null)
    {
        if (Maximum(perLap) <= 0 || nextStintLaps <= 0)
        {
            return Array.Empty<string>();
        }

        var required = RequiredForNextStint(
            current,
            perLap,
            nextStintLaps,
            limit).ToHashSet(StringComparer.Ordinal);
        var leftRequired = required.Contains("FL") || required.Contains("RL");
        var rightRequired = required.Contains("FR") || required.Contains("RR");
        if (leftRequired && rightRequired)
        {
            // Safety fallback when both sides are already unable to finish the
            // next stint. Normal planning services one side earlier so this
            // four-tire cold-start case is avoided rather than scheduled.
            return new[] { "FL", "FR", "RL", "RR" };
        }

        var horizon = nextStintLaps + followingStintLaps;
        var leftUrgency = SideUrgency(
            current.FrontLeftFraction, current.RearLeftFraction,
            perLap.FrontLeftFraction, perLap.RearLeftFraction,
            horizon, limit);
        var rightUrgency = SideUrgency(
            current.FrontRightFraction, current.RearRightFraction,
            perLap.FrontRightFraction, perLap.RearRightFraction,
            horizon, limit);

        if (leftRequired)
        {
            return new[] { "FL", "RL" };
        }
        if (rightRequired)
        {
            return new[] { "FR", "RR" };
        }

        var previousWasLeft = previousService?.Contains("FL") == true ||
            previousService?.Contains("RL") == true;
        var previousWasRight = previousService?.Contains("FR") == true ||
            previousService?.Contains("RR") == true;
        if (previousWasLeft && !previousWasRight)
        {
            return new[] { "FR", "RR" };
        }
        if (previousWasRight && !previousWasLeft)
        {
            return new[] { "FL", "RL" };
        }

        // First service starts with the side that is projected to be most
        // depleted. Subsequent calls alternate sides, limiting each pair to
        // the unavoidable two-stint cycle while avoiding four cold tires at once.
        return leftUrgency >= rightUrgency
            ? new[] { "FL", "RL" }
            : new[] { "FR", "RR" };
    }

    public static double EquivalentAgeLaps(
        LmuWheelWear consumed,
        LmuWheelWear perLap) => Math.Max(
        Math.Max(
            Age(consumed.FrontLeftFraction, perLap.FrontLeftFraction),
            Age(consumed.FrontRightFraction, perLap.FrontRightFraction)),
        Math.Max(
            Age(consumed.RearLeftFraction, perLap.RearLeftFraction),
            Age(consumed.RearRightFraction, perLap.RearRightFraction)));

    public static LmuWheelWear ResetChanged(
        LmuWheelWear wear,
        IReadOnlyList<string> tires) => new(
        tires.Contains("FL") ? 0 : wear.FrontLeftFraction,
        tires.Contains("FR") ? 0 : wear.FrontRightFraction,
        tires.Contains("RL") ? 0 : wear.RearLeftFraction,
        tires.Contains("RR") ? 0 : wear.RearRightFraction);

    private static LmuWheelWear Uniform(double value) => new(value, value, value, value);

    private static double SideUrgency(
        double frontWear,
        double rearWear,
        double frontPerLap,
        double rearPerLap,
        int laps,
        double limit) => Math.Max(
        frontWear + frontPerLap * laps,
        rearWear + rearPerLap * laps) / Math.Max(0.01, limit);

    private static IReadOnlyList<string> Ordered(ISet<string> tires) =>
        new[] { "FL", "FR", "RL", "RR" }
            .Where(tires.Contains)
            .ToArray();

    private static double Age(double consumed, double perLap) =>
        perLap > 0 ? Math.Max(0, consumed) / perLap : 0;
}
