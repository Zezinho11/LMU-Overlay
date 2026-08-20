using System.Collections;

namespace LmuOverlay.Strategy.Learning;

public sealed class RobustSampleWindow : IEnumerable<double>
{
    private readonly Queue<double> _values = new();

    public RobustSampleWindow(int capacity = 12)
    {
        Capacity = Math.Max(1, capacity);
    }

    public int Capacity { get; }
    public int Count => _values.Count;
    public double Median => RobustStatistics.Median(_values);
    public double Sigma => RobustStatistics.Sigma(_values);
    public double Conservative => Median + Sigma * 0.5;
    public double Trend => RobustStatistics.LinearTrend(_values);

    public void Add(double value)
    {
        _values.Enqueue(value);
        while (_values.Count > Capacity)
        {
            _values.Dequeue();
        }
    }

    public void Clear() => _values.Clear();
    public IEnumerator<double> GetEnumerator() => _values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public static class RobustStatistics
{
    public static double Median(IEnumerable<double> samples)
    {
        var ordered = samples.OrderBy(value => value).ToArray();
        return ordered.Length == 0
            ? 0
            : ordered.Length % 2 == 0
                ? (ordered[ordered.Length / 2 - 1] + ordered[ordered.Length / 2]) / 2
                : ordered[ordered.Length / 2];
    }

    public static double Sigma(IEnumerable<double> samples)
    {
        var values = samples.ToArray();
        if (values.Length < 2) return 0;
        var median = Median(values);
        var mad = Median(values.Select(value => Math.Abs(value - median)));
        return mad * 1.4826;
    }

    public static double LinearTrend(IEnumerable<double> samples)
    {
        var values = samples.ToArray();
        if (values.Length < 3) return 0;
        var xMean = (values.Length - 1) / 2d;
        var yMean = values.Average();
        var numerator = 0d;
        var denominator = 0d;
        for (var index = 0; index < values.Length; index++)
        {
            var x = index - xMean;
            numerator += x * (values[index] - yMean);
            denominator += x * x;
        }

        return denominator > 0 ? numerator / denominator : 0;
    }
}
