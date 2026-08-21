using LmuOverlay.Domain;

namespace LmuOverlay.Widgets;

public sealed record TireStopPlan(int Lap, IReadOnlyList<string> Tires)
{
    public bool ChangesTires => Tires.Count > 0;
    public string Action => ChangesTires ? string.Join("+", Tires) : "KEEP";
}

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
    int AvailableTireSets)
{
    public double FixedDurationSeconds { get; init; }
    public int CurrentVirtualEnergyRangeLaps { get; init; } = int.MaxValue;
    public int MaximumVirtualEnergyStintLaps { get; init; } = int.MaxValue;
    public double CurrentFuelLiters { get; init; }
    public double CurrentVirtualEnergyFraction { get; init; }
    public double VirtualEnergyFractionPerLap { get; init; }
    public double VirtualEnergyReserveFraction { get; init; }
    public LmuWheelWear CurrentTireWear { get; init; }
    public LmuWheelWear TireWearPerLap { get; init; }
}

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
    string AlternativeSummary)
{
    public int PitWindowStartLap { get; init; }
    public int PitWindowEndLap { get; init; }
    public string Explanation { get; init; } = string.Empty;
    public IReadOnlyList<TireStopPlan> TireStops { get; init; } =
        Array.Empty<TireStopPlan>();
}

public sealed record FuelSaveStrategyPlan(
    bool Available,
    double TargetConsumptionLitersPerLap,
    double SavingFraction,
    bool ReducesStopCount,
    EnduranceStrategyPlan Strategy,
    string Summary,
    string PitPlan,
    string TirePlan)
{
    public IReadOnlyList<FuelSaveStintInstruction> StintInstructions { get; init; } =
        Array.Empty<FuelSaveStintInstruction>();
    public string SaveLapPlan { get; init; } = string.Empty;
}

public sealed record FuelSaveStintInstruction(
    int StintNumber,
    int StintLaps,
    int SaveTargetLaps,
    double SaveLapTargetLiters);

public static class FullPushStrategyCalculator
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
        var energyMaximum = input.MaximumVirtualEnergyStintLaps is > 0 and < int.MaxValue
            ? input.MaximumVirtualEnergyStintLaps
            : int.MaxValue;
        var configuredMaximum = input.ConfiguredMaximumStintLaps > 0
            ? input.ConfiguredMaximumStintLaps
            : int.MaxValue;
        var maximumStint = Math.Min(Math.Min(fuelMaximum, energyMaximum), configuredMaximum);
        var currentResourceRange = input.CurrentVirtualEnergyRangeLaps is > 0 and < int.MaxValue
            ? Math.Min(input.CurrentFuelRangeLaps, input.CurrentVirtualEnergyRangeLaps)
            : input.CurrentFuelRangeLaps;
        var firstStint = Math.Clamp(
            Math.Max(1, currentResourceRange),
            1,
            Math.Min(maximumStint, input.RemainingLaps));
        var afterFirst = input.RemainingLaps - firstStint;
        var minimumFutureStints = afterFirst > 0
            ? (int)Math.Ceiling(afterFirst / (double)maximumStint)
            : 0;
        var candidates = new List<Candidate>();
        // Tire life can require several service stops beyond the fuel minimum
        // in a long event. Keep searching until a complete race plan is found.
        var maximumAdditionalStops = Math.Min(24, Math.Max(2, afterFirst));
        for (var extraStops = 0; extraStops <= maximumAdditionalStops; extraStops++)
        {
            var futureStints = minimumFutureStints + extraStops;
            if (afterFirst == 0 && futureStints > 0 ||
                afterFirst > 0 && futureStints <= 0)
            {
                continue;
            }

            var stints = extraStops == 0
                ? BuildFuelLedStints(firstStint, afterFirst, futureStints, maximumStint)
                : BuildBalancedStints(firstStint, afterFirst, futureStints);
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
        var displayedDuration = input.FixedDurationSeconds > 0
            ? input.FixedDurationSeconds
            : best.EstimatedTimeSeconds;
        var firstPit = best.PitLaps.FirstOrDefault();
        return new(
            true,
            best.Stints.Count,
            best.Stints.Count - 1,
            best.TireSets,
            displayedDuration,
            best.Stints,
            best.PitLaps,
            best.TireChangeLaps,
            best.FuelAtStops,
            $"FULL PUSH · {best.Stints.Count} STINTS · {best.Stints.Count - 1} STOPS · " +
            $"{FormatDuration(displayedDuration)}",
            best.PitLaps.Count == 0
                ? "NO MORE STOPS"
                : string.Join(" · ", best.PitLaps.Select((lap, index) =>
                    $"L{lap} +{best.FuelAtStops[index]:0.0}L")),
            TireSummary(best, input.AvailableTireSets),
            alternative is null
                ? "NO VIABLE ALTERNATIVE"
                : $"ALT {alternative.Stints.Count} STINTS · " +
                  $"{FormatDuration(alternative.EstimatedTimeSeconds)} · " +
                  $"{alternative.EstimatedTimeSeconds - best.EstimatedTimeSeconds:+0;-0;0}s")
        {
            PitWindowStartLap = firstPit > 0 ? Math.Max(input.CompletedLaps + 1, firstPit - 1) : 0,
            PitWindowEndLap = firstPit > 0 ? firstPit + 1 : 0,
            Explanation = $"RESOURCE LIMIT {maximumStint} LAPS · " +
                $"FUEL {fuelMaximum} · ENERGY {(energyMaximum == int.MaxValue ? "N/A" : energyMaximum)} · " +
                $"RESERVE {input.ReserveFuelLiters:0.0} L · {candidates.Count} CANDIDATES",
            TireStops = best.TireStops,
        };
    }

    private static Candidate? Evaluate(
        EnduranceStrategyInput input,
        IReadOnlyList<int> stints)
    {
        var wheelWearPerLap = TireStrategyCalculator.WearPerLap(input);
        var wearPerLap = TireStrategyCalculator.Maximum(wheelWearPerLap);
        var wearLimit = Math.Clamp(input.TireWearLimitFraction, 0.2, 0.95);
        var tireChanges = new List<int>();
        var tireStops = new List<TireStopPlan>();
        var currentWear = TireStrategyCalculator.CurrentWear(input);
        var cumulativeLaps = input.CompletedLaps;
        var changedTireCount = 0;
        var tireServiceSeconds = 0d;
        var degradationLoss = 0d;
        var pitLaps = new List<int>();
        var fuelAtStops = new List<double>();

        for (var stintIndex = 0; stintIndex < stints.Count; stintIndex++)
        {
            var stintLaps = stints[stintIndex];
            if (wearPerLap > 0 && TireStrategyCalculator.WouldExceed(
                    currentWear,
                    wheelWearPerLap,
                    stintLaps,
                    wearLimit))
            {
                return null;
            }

            for (var lap = 0; lap < stintLaps; lap++)
            {
                degradationLoss += Math.Max(0, input.PaceDegradationSecondsPerLap) *
                    TireStrategyCalculator.EquivalentAgeLaps(
                        currentWear,
                        wheelWearPerLap);
                currentWear = TireStrategyCalculator.AddWear(
                    currentWear,
                    wheelWearPerLap,
                    1);
            }
            cumulativeLaps += stintLaps;
            if (stintIndex < stints.Count - 1)
            {
                pitLaps.Add(cumulativeLaps);
                var nextStintIndex = stintIndex + 1;
                var nextStint = stints[nextStintIndex];
                var followingStint = nextStintIndex + 1 < stints.Count
                    ? stints[nextStintIndex + 1]
                    : 0;
                var tires = TireStrategyCalculator.ServiceForUpcomingStints(
                    currentWear,
                    wheelWearPerLap,
                    nextStint,
                    followingStint,
                    wearLimit,
                    tireStops.LastOrDefault(item => item.Tires.Count > 0)?.Tires);
                tireStops.Add(new(cumulativeLaps, tires));
                if (tires.Count > 0)
                {
                    tireChanges.Add(cumulativeLaps);
                    changedTireCount += tires.Count;
                    tireServiceSeconds += Math.Max(0, input.TireChangeSeconds) *
                        tires.Count / 4d;
                    currentWear = TireStrategyCalculator.ResetChanged(currentWear, tires);
                }
                var isFinalFill = nextStintIndex == stints.Count - 1;
                var reserve = isFinalFill
                    ? input.ReserveFuelLiters
                    : 0;
                // Add only what the following stint needs. NRG can make the
                // effective tank smaller than the car's physical tank.
                var fuelTarget = Math.Min(
                    input.FuelCapacityLiters,
                    nextStint * input.ConsumptionLitersPerLap + reserve);
                var previousTarget = stintIndex == 0
                    ? input.CurrentFuelLiters
                    : Math.Min(
                        input.FuelCapacityLiters,
                        stintLaps * input.ConsumptionLitersPerLap);
                var fuelExpectedAtStop = Math.Max(
                    0,
                    previousTarget - stintLaps * input.ConsumptionLitersPerLap);
                fuelAtStops.Add(Math.Max(0, fuelTarget - fuelExpectedAtStop));
            }
        }

        var tireSets = 1 + (int)Math.Ceiling(changedTireCount / 4d);
        if (input.AvailableTireSets > 0 && tireSets > input.AvailableTireSets)
        {
            return null;
        }

        var stops = Math.Max(0, stints.Count - 1);
        var estimated = input.RemainingLaps * input.ReferencePaceSeconds +
            stops * Math.Max(0, input.PitLossSeconds) +
            tireServiceSeconds +
            degradationLoss;
        return new(
            stints.ToArray(),
            pitLaps,
            tireChanges,
            tireStops,
            fuelAtStops,
            tireSets,
            estimated,
            wearPerLap > 0);
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

    private static IReadOnlyList<int> BuildFuelLedStints(
        int firstStint,
        int remainingLaps,
        int futureStints,
        int maximumStint)
    {
        var result = new List<int> { firstStint };
        for (var index = 0; index < futureStints; index++)
        {
            var slotsAfter = futureStints - index - 1;
            var laps = Math.Min(maximumStint, remainingLaps - slotsAfter);
            result.Add(laps);
            remainingLaps -= laps;
        }
        return result;
    }

    private static string TireSummary(Candidate candidate, int availableSets)
    {
        if (!candidate.TireDataAvailable)
        {
            return candidate.PitLaps.Count == 0
                ? "TIRE LEARNING · NO MORE STOPS"
                : "TIRE LEARNING · " + string.Join(" · ",
                    candidate.PitLaps.Select(lap => $"L{lap} TBD"));
        }

        var allocation = availableSets > 0
            ? $"{candidate.TireSets}/{availableSets} SETS"
            : $"{candidate.TireSets} SETS";
        if (candidate.TireStops.Count == 0)
        {
            return $"{allocation} · NO MORE STOPS";
        }

        return $"{allocation} · " + string.Join(" · ", candidate.TireStops.Select(stop =>
            $"L{stop.Lap} {stop.Action}"));
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
        IReadOnlyList<TireStopPlan> TireStops,
        IReadOnlyList<double> FuelAtStops,
        int TireSets,
        double EstimatedTimeSeconds,
        bool TireDataAvailable);
}
