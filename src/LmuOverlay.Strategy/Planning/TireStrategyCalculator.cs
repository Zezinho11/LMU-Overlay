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

    public static LmuWheelWear ResetChanged(
        LmuWheelWear wear,
        IReadOnlyList<string> tires) => new(
        tires.Contains("FL") ? 0 : wear.FrontLeftFraction,
        tires.Contains("FR") ? 0 : wear.FrontRightFraction,
        tires.Contains("RL") ? 0 : wear.RearLeftFraction,
        tires.Contains("RR") ? 0 : wear.RearRightFraction);

    private static LmuWheelWear Uniform(double value) => new(value, value, value, value);
}
