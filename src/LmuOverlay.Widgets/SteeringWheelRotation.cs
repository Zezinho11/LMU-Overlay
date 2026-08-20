namespace LmuOverlay.Widgets;

public static class SteeringWheelRotation
{
    public const double DefaultRangeDegrees = 540;

    public static double ResolveRangeDegrees(double physical, double visual)
    {
        if (Plausible(physical))
        {
            return physical;
        }
        return Plausible(visual) ? visual : DefaultRangeDegrees;
    }

    public static double AngleDegrees(double steering, double rangeDegrees) =>
        Math.Clamp(steering, -1, 1) *
        ResolveRangeDegrees(rangeDegrees, 0) /
        2;

    private static bool Plausible(double value) =>
        double.IsFinite(value) && value is >= 180 and <= 1_440;
}
