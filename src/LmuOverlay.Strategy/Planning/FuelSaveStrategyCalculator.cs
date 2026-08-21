namespace LmuOverlay.Widgets;

public static class FuelSaveStrategyCalculator
{
    public static FuelSaveStrategyPlan Calculate(
        EnduranceStrategyInput input,
        EnduranceStrategyPlan fullPush,
        double currentFuelLiters,
        double maximumSavingFraction = 0.15)
    {
        if (!fullPush.Available || fullPush.Stops <= 0 || input.RemainingLaps <= 0)
        {
            return Unavailable(input, "FUEL SAVE · NO STOP TO REMOVE");
        }

        var targetStops = fullPush.Stops - 1;
        var usableFuel = Math.Max(0, currentFuelLiters) +
            targetStops * Math.Max(0, input.FuelCapacityLiters) -
            Math.Max(0, input.ReserveFuelLiters);
        var targetConsumption = usableFuel / input.RemainingLaps;
        var saving = input.ConsumptionLitersPerLap > 0
            ? 1 - targetConsumption / input.ConsumptionLitersPerLap
            : 0;

        if (targetConsumption > 0 && saving > 0 && saving <= maximumSavingFraction)
        {
            for (var attemptedSaving = saving;
                 attemptedSaving <= maximumSavingFraction + 0.0001;
                 attemptedSaving += 0.0025)
            {
                var attemptedTarget = input.ConsumptionLitersPerLap * (1 - attemptedSaving);
                var reducedStops = CalculateForConsumption(input, currentFuelLiters, attemptedTarget);
                if (reducedStops.Available && reducedStops.Stops <= targetStops)
                {
                    if (!CanExpressWithLmuTargets(input, reducedStops))
                    {
                        continue;
                    }

                    return Result(
                        input,
                        reducedStops,
                        input.ConsumptionLitersPerLap,
                        attemptedTarget,
                        attemptedSaving,
                        true);
                }
            }
        }

        var currentResourceRange = Math.Min(
            Math.Max(1, input.CurrentFuelRangeLaps),
            input.CurrentVirtualEnergyRangeLaps is > 0 and < int.MaxValue
                ? input.CurrentVirtualEnergyRangeLaps
                : int.MaxValue);
        var desiredRange = currentResourceRange + 1;
        var fuelTarget = Math.Max(0, currentFuelLiters - input.ReserveFuelLiters) /
            desiredRange;
        var fuelSaving = input.ConsumptionLitersPerLap > 0
            ? 1 - fuelTarget / input.ConsumptionLitersPerLap
            : 0;
        var energyTarget = input.VirtualEnergyFractionPerLap > 0
            ? Math.Max(0, input.CurrentVirtualEnergyFraction -
                input.VirtualEnergyReserveFraction) / desiredRange
            : 0;
        var energySaving = input.VirtualEnergyFractionPerLap > 0
            ? 1 - energyTarget / input.VirtualEnergyFractionPerLap
            : 0;
        var extensionSaving = Math.Max(fuelSaving, energySaving);
        var attemptedSavings = new List<double>();
        if (extensionSaving is > 0 and <= 1) attemptedSavings.Add(extensionSaving);
        attemptedSavings.AddRange(new[] { 0.025, 0.05, 0.075, 0.10, 0.125, 0.15 });

        foreach (var attemptedSaving in attemptedSavings
                     .Where(value => value > 0 && value <= maximumSavingFraction + 0.0001)
                     .Distinct()
                     .OrderBy(value => value))
        {
            var attemptedTarget = input.ConsumptionLitersPerLap * (1 - attemptedSaving);
            var alternative = CalculateForConsumption(input, currentFuelLiters, attemptedTarget);
            if (alternative.Available && alternative.Stops <= fullPush.Stops)
            {
                if (!CanExpressWithLmuTargets(input, alternative))
                {
                    continue;
                }

                return Result(
                    input,
                    alternative,
                    input.ConsumptionLitersPerLap,
                    attemptedTarget,
                    attemptedSaving,
                    alternative.Stops < fullPush.Stops);
            }
        }

        return Unavailable(input, "FUEL SAVE · NO SAFE TARGET WITH LIVE RESOURCES");
    }

    private static EnduranceStrategyPlan CalculateForConsumption(
        EnduranceStrategyInput input,
        double currentFuelLiters,
        double consumption)
    {
        var currentRange = Math.Max(1, (int)Math.Floor(
            Math.Max(0, currentFuelLiters - input.ReserveFuelLiters) / consumption));
        var maximumRange = Math.Max(1, (int)Math.Floor(
            input.FuelCapacityLiters / consumption));
        var consumptionRatio = input.ConsumptionLitersPerLap > 0
            ? consumption / input.ConsumptionLitersPerLap
            : 1;
        var energyPerLap = input.VirtualEnergyFractionPerLap > 0
            ? input.VirtualEnergyFractionPerLap * consumptionRatio
            : 0;
        var currentEnergyRange = energyPerLap > 0
            ? Math.Max(1, (int)Math.Floor(
                Math.Max(0, input.CurrentVirtualEnergyFraction -
                    input.VirtualEnergyReserveFraction) / energyPerLap))
            : int.MaxValue;
        var maximumEnergyRange = energyPerLap > 0
            ? Math.Max(1, (int)Math.Floor(
                Math.Max(0, 1 - input.VirtualEnergyReserveFraction) / energyPerLap))
            : int.MaxValue;
        return EnduranceStrategyPlanner.Calculate(input with
        {
            CurrentFuelRangeLaps = currentRange,
            MaximumFuelStintLaps = maximumRange,
            ConsumptionLitersPerLap = consumption,
            CurrentVirtualEnergyRangeLaps = currentEnergyRange,
            MaximumVirtualEnergyStintLaps = maximumEnergyRange,
            VirtualEnergyFractionPerLap = energyPerLap,
        });
    }

    private static FuelSaveStrategyPlan Result(
        EnduranceStrategyInput input,
        EnduranceStrategyPlan strategy,
        double pushConsumption,
        double targetConsumption,
        double saving,
        bool reducesStops)
    {
        var instructions = BuildStintInstructions(
            input,
            strategy.StintLaps,
            pushConsumption);
        return new(
            true,
            targetConsumption,
            saving,
            reducesStops,
            strategy,
            $"FUEL SAVE · {strategy.Stints} STINTS · {strategy.Stops} STOPS · " +
            (reducesStops ? "FEWER STOPS" : "SAME STOPS"),
            strategy.PitPlan,
            strategy.TirePlan)
        {
            StintInstructions = instructions,
            SaveLapPlan = FormatSaveLapPlan(instructions),
        };
    }

    private static IReadOnlyList<FuelSaveStintInstruction> BuildStintInstructions(
        EnduranceStrategyInput input,
        IReadOnlyList<int> stintLaps,
        double pushConsumption)
    {
        var currentRange = CurrentPushRange(input);
        var futureRange = FuturePushRange(input);
        return stintLaps.Select((laps, index) =>
        {
            var pushRange = index == 0 ? currentRange : futureRange;
            var saveTarget = Math.Clamp(laps - pushRange, 0, 3);
            return new FuelSaveStintInstruction(
                index + 1,
                laps,
                saveTarget,
                saveTarget > 0
                    ? pushConsumption * pushRange / (pushRange + saveTarget)
                    : pushConsumption);
        }).Where(item => item.SaveTargetLaps > 0).ToArray();
    }

    private static string FormatSaveLapPlan(
        IReadOnlyList<FuelSaveStintInstruction> instructions)
    {
        if (instructions.Count == 0)
        {
            return "SAVE TARGET · NO STINT DATA";
        }

        var groups = new List<(int First, int Last, FuelSaveStintInstruction Item)>();
        foreach (var item in instructions)
        {
            if (groups.Count > 0 && SameInstruction(groups[^1].Item, item))
            {
                groups[^1] = (groups[^1].First, item.StintNumber, groups[^1].Item);
            }
            else
            {
                groups.Add((item.StintNumber, item.StintNumber, item));
            }
        }

        return "SAVE TARGET · " + string.Join(" · ", groups.Select(group =>
        {
            var stint = group.First == group.Last
                ? $"S{group.First}"
                : $"S{group.First}-{group.Last}";
            var suffix = group.Item.SaveTargetLaps == 1 ? "LAP" : "LAPS";
            return $"{stint} +{group.Item.SaveTargetLaps} {suffix}";
        }));
    }

    private static bool SameInstruction(
        FuelSaveStintInstruction first,
        FuelSaveStintInstruction second) =>
        first.SaveTargetLaps == second.SaveTargetLaps;

    private static bool CanExpressWithLmuTargets(
        EnduranceStrategyInput input,
        EnduranceStrategyPlan strategy)
    {
        var currentRange = CurrentPushRange(input);
        var futureRange = FuturePushRange(input);
        var hasTarget = false;
        for (var index = 0; index < strategy.StintLaps.Count; index++)
        {
            var pushRange = index == 0 ? currentRange : futureRange;
            var extension = strategy.StintLaps[index] - pushRange;
            if (extension > 3)
            {
                return false;
            }
            hasTarget |= extension > 0;
        }
        return hasTarget;
    }

    private static int CurrentPushRange(EnduranceStrategyInput input)
    {
        var energy = input.CurrentVirtualEnergyRangeLaps is > 0 and < int.MaxValue
            ? input.CurrentVirtualEnergyRangeLaps
            : int.MaxValue;
        var configured = input.ConfiguredMaximumStintLaps > 0
            ? input.ConfiguredMaximumStintLaps
            : int.MaxValue;
        return Math.Max(1, Math.Min(Math.Min(input.CurrentFuelRangeLaps, energy), configured));
    }

    private static int FuturePushRange(EnduranceStrategyInput input)
    {
        var energy = input.MaximumVirtualEnergyStintLaps is > 0 and < int.MaxValue
            ? input.MaximumVirtualEnergyStintLaps
            : int.MaxValue;
        var configured = input.ConfiguredMaximumStintLaps > 0
            ? input.ConfiguredMaximumStintLaps
            : int.MaxValue;
        return Math.Max(1, Math.Min(Math.Min(input.MaximumFuelStintLaps, energy), configured));
    }

    private static FuelSaveStrategyPlan Unavailable(
        EnduranceStrategyInput input,
        string summary) => new(
        false,
        0,
        0,
        false,
        EnduranceStrategyPlanner.Calculate(input with { RemainingLaps = 0 }),
        summary,
        "TARGET DATA REQUIRED",
        "KEEP FULL-PUSH TYRE PLAN");
}
