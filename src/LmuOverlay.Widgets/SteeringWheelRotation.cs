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

    public static double ResolveDisplayRangeDegrees(
        double visual,
        double physical,
        double manual = 0)
    {
        // LMU normalizes mUnfilteredSteering against the car steering lock.
        // Its visual range is consequently the matching angular reference;
        // physical is the controller capability and can be much larger.
        // Some cars publish unreliable ranges, so a profile override wins.
        if (Plausible(manual))
        {
            return manual;
        }

        return ResolveRangeDegrees(visual, physical);
    }

    public static double ResolvePhysicalRangeDegrees(
        double physical,
        double visual,
        double manual = 0) =>
        Plausible(manual)
            ? manual
            : ResolveRangeDegrees(physical, visual);

    public static double ResolveSynchronizedRangeDegrees(
        double directSteering,
        double simulatorSteering,
        double visual,
        double physical,
        double manual = 0)
    {
        if (Plausible(manual))
        {
            return manual;
        }

        // Windows reports a fraction of the controller lock while LMU reports
        // a fraction of the active car lock. Their ratio recovers the physical
        // range without assuming that every car or wheel uses 540/900 degrees.
        if (Math.Abs(directSteering) >= 0.02 &&
            Math.Abs(simulatorSteering) >= 0.02 &&
            Math.Sign(directSteering) == Math.Sign(simulatorSteering))
        {
            // This widget represents the user's physical rim. LMU publishes a
            // dedicated physical range for that purpose; visual range belongs
            // to the cockpit animation and can differ slightly by vehicle.
            var carRange = ResolveRangeDegrees(physical, visual);
            var synchronized = Math.Abs(simulatorSteering / directSteering) * carRange;
            if (Plausible(synchronized))
            {
                return synchronized;
            }
        }

        return ResolvePhysicalRangeDegrees(physical, visual);
    }

    public static double AngleDegrees(double steering, double rangeDegrees) =>
        Math.Clamp(steering, -1, 1) *
        ResolveRangeDegrees(rangeDegrees, 0) /
        2;

    private static bool Plausible(double value) =>
        double.IsFinite(value) && value is >= 180 and <= 1_440;
}
