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
    public static TireTemperatureBand Classify(double celsius) =>
        !double.IsFinite(celsius) || celsius <= 0
            ? TireTemperatureBand.Unknown
            : celsius < 60
                ? TireTemperatureBand.Cold
                : celsius < 75
                    ? TireTemperatureBand.Warming
                    : celsius < 100
                        ? TireTemperatureBand.Optimal
                        : celsius < 115
                            ? TireTemperatureBand.Hot
                            : TireTemperatureBand.Critical;

    public static TireTemperatureBand ClassifyStable(
        double celsius,
        TireTemperatureBand current,
        double hysteresisCelsius = 2)
    {
        var candidate = Classify(celsius);
        if (current == TireTemperatureBand.Unknown ||
            candidate == TireTemperatureBand.Unknown ||
            candidate == current)
        {
            return candidate;
        }

        var boundary = (current, candidate) switch
        {
            (TireTemperatureBand.Cold, TireTemperatureBand.Warming) or
            (TireTemperatureBand.Warming, TireTemperatureBand.Cold) => 60,
            (TireTemperatureBand.Warming, TireTemperatureBand.Optimal) or
            (TireTemperatureBand.Optimal, TireTemperatureBand.Warming) => 75,
            (TireTemperatureBand.Optimal, TireTemperatureBand.Hot) or
            (TireTemperatureBand.Hot, TireTemperatureBand.Optimal) => 100,
            (TireTemperatureBand.Hot, TireTemperatureBand.Critical) or
            (TireTemperatureBand.Critical, TireTemperatureBand.Hot) => 115,
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
