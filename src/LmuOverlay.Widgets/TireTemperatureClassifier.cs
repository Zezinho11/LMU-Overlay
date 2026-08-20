namespace LmuOverlay.Widgets;

public enum TireTemperatureBand
{
    Unknown,
    Cold,
    Warming,
    Optimal,
    Hot,
    Critical,
}

public static class TireTemperatureClassifier
{
    public static TireTemperatureBand Classify(
        double celsius,
        TireTemperatureProfile? configuredProfile = null)
    {
        var profile = (configuredProfile ?? TireTemperatureProfile.Default).Sanitize();
        return
        !double.IsFinite(celsius) || celsius <= 0
            ? TireTemperatureBand.Unknown
            : celsius < profile.ColdToWarming
                ? TireTemperatureBand.Cold
                : celsius < profile.WarmingToOptimal
                    ? TireTemperatureBand.Warming
                    : celsius < profile.OptimalToHot
                        ? TireTemperatureBand.Optimal
                        : celsius < profile.HotToCritical
                            ? TireTemperatureBand.Hot
                            : TireTemperatureBand.Critical;
    }

    public static TireTemperatureBand ClassifyStable(
        double celsius,
        TireTemperatureBand current,
        double hysteresisCelsius = 2,
        TireTemperatureProfile? configuredProfile = null)
    {
        var profile = (configuredProfile ?? TireTemperatureProfile.Default).Sanitize();
        var candidate = Classify(celsius, profile);
        if (current == TireTemperatureBand.Unknown ||
            candidate == TireTemperatureBand.Unknown ||
            candidate == current)
        {
            return candidate;
        }

        var boundary = (current, candidate) switch
        {
            (TireTemperatureBand.Cold, TireTemperatureBand.Warming) or
            (TireTemperatureBand.Warming, TireTemperatureBand.Cold) => profile.ColdToWarming,
            (TireTemperatureBand.Warming, TireTemperatureBand.Optimal) or
            (TireTemperatureBand.Optimal, TireTemperatureBand.Warming) => profile.WarmingToOptimal,
            (TireTemperatureBand.Optimal, TireTemperatureBand.Hot) or
            (TireTemperatureBand.Hot, TireTemperatureBand.Optimal) => profile.OptimalToHot,
            (TireTemperatureBand.Hot, TireTemperatureBand.Critical) or
            (TireTemperatureBand.Critical, TireTemperatureBand.Hot) => profile.HotToCritical,
            _ => double.NaN,
        };
        if (!double.IsFinite(boundary))
        {
            return candidate;
        }

        return (int)candidate > (int)current
            ? celsius >= boundary + hysteresisCelsius ? candidate : current
            : celsius < boundary - hysteresisCelsius ? candidate : current;
    }
}
