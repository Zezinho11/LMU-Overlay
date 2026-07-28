namespace LmuOverlay.Core;

public sealed record RuntimePerformanceBudgetResult(
    bool WithinBudget,
    bool AverageReadWithinBudget,
    bool MaximumReadWithinBudget,
    bool WorkingSetWithinBudget,
    double AverageReadLimitMilliseconds,
    double MaximumReadLimitMilliseconds,
    double WorkingSetLimitMegabytes);

public static class RuntimePerformanceBudget
{
    public const double AverageReadLimitMilliseconds = 5;
    public const double MaximumReadLimitMilliseconds = 25;
    public const long WorkingSetLimitBytes = 250L * 1024 * 1024;

    public static RuntimePerformanceBudgetResult Evaluate(
        TelemetryRuntimeHealth health,
        long workingSetBytes)
    {
        var averageOk = health.AverageReadMilliseconds <= AverageReadLimitMilliseconds;
        var maximumOk = health.MaximumReadMilliseconds <= MaximumReadLimitMilliseconds;
        var memoryOk = workingSetBytes <= WorkingSetLimitBytes;
        return new(
            averageOk && maximumOk && memoryOk,
            averageOk,
            maximumOk,
            memoryOk,
            AverageReadLimitMilliseconds,
            MaximumReadLimitMilliseconds,
            WorkingSetLimitBytes / 1024d / 1024d);
    }
}
