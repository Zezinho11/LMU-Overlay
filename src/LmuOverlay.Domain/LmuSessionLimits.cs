namespace LmuOverlay.Domain;

public static class LmuSessionLimits
{
    public static bool HasFiniteLapLimit(int maximumLaps) =>
        maximumLaps is > 0 and < int.MaxValue;

    public static int NormalizeMaximumLaps(int maximumLaps) =>
        HasFiniteLapLimit(maximumLaps) ? maximumLaps : 0;
}
