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
}
