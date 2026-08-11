namespace LmuOverlay.Widgets;

public sealed record EnduranceStrategyInput(
    int CompletedLaps,
    int RemainingLaps,
    int CurrentFuelRangeLaps,
    int MaximumFuelStintLaps,
    int ConfiguredMaximumStintLaps,
    double ReferencePaceSeconds,
    double PaceDegradationSecondsPerLap,
    double ConsumptionLitersPerLap,
    double FuelCapacityLiters,
    double ReserveFuelLiters,
    double PitLossSeconds,
    double TireChangeSeconds,
    double CurrentMaximumTireWearFraction,
    double TireWearFractionPerLap,
    double TireWearLimitFraction,
    int AvailableTireSets);

public sealed record EnduranceStrategyPlan(
    bool Available,
    int Stints,
    int Stops,
    int TireSets,
    double EstimatedRaceTimeSeconds,
    IReadOnlyList<int> StintLaps,
    IReadOnlyList<int> PitLaps,
    IReadOnlyList<int> TireChangeLaps,
    IReadOnlyList<double> FuelAtStopsLiters,
    string Summary,
    string PitPlan,
    string TirePlan,
    string AlternativeSummary);

public static class EnduranceStrategyPlanner
{
    public static EnduranceStrategyPlan Calculate(EnduranceStrategyInput input)
    {
        if (input.RemainingLaps <= 0 ||
            input.ReferencePaceSeconds <= 0 ||
            input.ConsumptionLitersPerLap <= 0)
        {
            return Unavailable();
        }

        var fuelMaximum = Math.Max(1, input.MaximumFuelStintLaps);
        var configuredMaximum = input.ConfiguredMaximumStintLaps > 0
            ? input.ConfiguredMaximumStintLaps
            : int.MaxValue;
        var maximumStint = Math.Min(fuelMaximum, configuredMaximum);
        var firstStint = Math.Clamp(
            Math.Max(1, input.CurrentFuelRangeLaps),
            1,
            Math.Min(maximumStint, input.RemainingLaps));
        var afterFirst = input.RemainingLaps - firstStint;
        var minimumFutureStints = afterFirst > 0
            ? (int)Math.Ceiling(afterFirst / (double)maximumStint)
            : 0;
        var candidates = new List<Candidate>();
        for (var extraStops = 0; extraStops <= 2; extraStops++)
        {
            var futureStints = minimumFutureStints + extraStops;
            if (afterFirst == 0 && futureStints > 0 ||
                afterFirst > 0 && futureStints <= 0)
            {
                continue;
            }

            var stints = BuildBalancedStints(firstStint, afterFirst, futureStints);
            if (stints.Any(laps => laps <= 0 || laps > maximumStint))
            {
                continue;
            }

            var candidate = Evaluate(input, stints);
            if (candidate is not null)
            {
                candidates.Add(candidate);
            }
        }

        var ordered = candidates
            .OrderBy(candidate => candidate.EstimatedTimeSeconds)
            .ThenBy(candidate => candidate.Stints.Count)
            .ToArray();
        if (ordered.Length == 0)
        {
            return Unavailable();
        }

        var best = ordered[0];
        var alternative = ordered.Skip(1).FirstOrDefault();
        return new(
            true,
            best.Stints.Count,
            best.Stints.Count - 1,
            best.TireSets,
            best.EstimatedTimeSeconds,
            best.Stints,
            best.PitLaps,
            best.TireChangeLaps,
            best.FuelAtStops,
            $"{best.Stints.Count} STINTS · {best.Stints.Count - 1} STOPS · " +
            $"{FormatDuration(best.EstimatedTimeSeconds)}",
            best.PitLaps.Count == 0
                ? "NO MORE STOPS"
                : string.Join(" · ", best.PitLaps.Select((lap, index) =>
                    $"L{lap} +{best.FuelAtStops[index]:0.0}L")),
            TireSummary(best, input.AvailableTireSets),
            alternative is null
                ? "NO VIABLE ALTERNATIVE"
                : $"ALT {alternative.Stints.Count} STINTS · " +
                  $"{FormatDuration(alternative.EstimatedTimeSeconds)} · " +
                  $"{alternative.EstimatedTimeSeconds - best.EstimatedTimeSeconds:+0;-0;0}s");
    }

    private static Candidate? Evaluate(
        EnduranceStrategyInput input,
        IReadOnlyList<int> stints)
    {
        var wearPerLap = Math.Max(0, input.TireWearFractionPerLap);
        var wearLimit = Math.Clamp(input.TireWearLimitFraction, 0.2, 0.95);
        var currentTireLapsRemaining = wearPerLap > 0
            ? Math.Max(0, (int)Math.Floor(
                (wearLimit - input.CurrentMaximumTireWearFraction) / wearPerLap))
            : int.MaxValue;
        var newTireCapacity = wearPerLap > 0
            ? Math.Max(1, (int)Math.Floor(wearLimit / wearPerLap))
            : int.MaxValue;
        if (stints[0] > currentTireLapsRemaining && wearPerLap > 0)
        {
            return null;
        }

        var tireChanges = new List<int>();
        var tireCapacityRemaining = currentTireLapsRemaining;
        var cumulativeLaps = input.CompletedLaps;
        var tireSets = 1;
        var tireAgeLaps = wearPerLap > 0
            ? Math.Max(0, input.CurrentMaximumTireWearFraction / wearPerLap)
            : 0;
        var degradationLoss = 0d;
        var pitLaps = new List<int>();
        var fuelAtStops = new List<double>();

        for (var stintIndex = 0; stintIndex < stints.Count; stintIndex++)
        {
            var stintLaps = stints[stintIndex];
            if (stintIndex > 0 && stintLaps > tireCapacityRemaining)
            {
                tireChanges.Add(cumulativeLaps);
                tireSets++;
                tireCapacityRemaining = newTireCapacity;
                tireAgeLaps = 0;
            }

            if (stintLaps > tireCapacityRemaining)
            {
                return null;
            }

            for (var lap = 0; lap < stintLaps; lap++)
            {
                degradationLoss += Math.Max(0, input.PaceDegradationSecondsPerLap) *
                    tireAgeLaps;
                tireAgeLaps++;
            }

            tireCapacityRemaining = tireCapacityRemaining == int.MaxValue
                ? int.MaxValue
                : tireCapacityRemaining - stintLaps;
            cumulativeLaps += stintLaps;
            if (stintIndex < stints.Count - 1)
            {
                pitLaps.Add(cumulativeLaps);
                var nextStint = stints[stintIndex + 1];
                var reserve = stintIndex + 1 == stints.Count - 1
                    ? input.ReserveFuelLiters
                    : 0;
                var fuelTarget = Math.Min(
                    input.FuelCapacityLiters,
                    nextStint * input.ConsumptionLitersPerLap + reserve);
                var fuelExpectedAtStop = stintIndex == 0
                    ? input.ReserveFuelLiters
                    : 0;
                fuelAtStops.Add(Math.Max(0, fuelTarget - fuelExpectedAtStop));
            }
        }

        if (input.AvailableTireSets > 0 && tireSets > input.AvailableTireSets)
        {
            return null;
        }

        var stops = Math.Max(0, stints.Count - 1);
        var estimated = input.RemainingLaps * input.ReferencePaceSeconds +
            stops * Math.Max(0, input.PitLossSeconds) +
            tireChanges.Count * Math.Max(0, input.TireChangeSeconds) +
            degradationLoss;
        return new(
            stints.ToArray(),
            pitLaps,
            tireChanges,
            fuelAtStops,
            tireSets,
            estimated);
    }

    private static IReadOnlyList<int> BuildBalancedStints(
        int firstStint,
        int remainingLaps,
        int futureStints)
    {
        var result = new List<int> { firstStint };
        if (futureStints <= 0)
        {
            return result;
        }

        var baseLength = remainingLaps / futureStints;
        var extra = remainingLaps % futureStints;
        for (var index = 0; index < futureStints; index++)
        {
            result.Add(baseLength + (index >= futureStints - extra ? 1 : 0));
        }

        return result;
    }

    private static string TireSummary(Candidate candidate, int availableSets)
    {
        var allocation = availableSets > 0
            ? $"{candidate.TireSets}/{availableSets} SETS"
            : $"{candidate.TireSets} SETS";
        return candidate.TireChangeLaps.Count == 0
            ? $"{allocation} · KEEP CURRENT SET"
            : $"{allocation} · CHANGE " +
              string.Join("/", candidate.TireChangeLaps.Select(lap => $"L{lap}"));
    }

    private static string FormatDuration(double seconds)
    {
        var duration = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}";
    }

    private static EnduranceStrategyPlan Unavailable() => new(
        false, 0, 0, 0, 0,
        Array.Empty<int>(),
        Array.Empty<int>(),
        Array.Empty<int>(),
        Array.Empty<double>(),
        "LEARNING STRATEGY",
        "NO PLAN",
        "TIRE DATA REQUIRED",
        "NO ALTERNATIVE");

    private sealed record Candidate(
        IReadOnlyList<int> Stints,
        IReadOnlyList<int> PitLaps,
        IReadOnlyList<int> TireChangeLaps,
        IReadOnlyList<double> FuelAtStops,
        int TireSets,
        double EstimatedTimeSeconds);
}
