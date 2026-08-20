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
                    return Result(reducedStops, attemptedTarget, attemptedSaving, true);
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
                return Result(
                    alternative,
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
        EnduranceStrategyPlan strategy,
        double targetConsumption,
        double saving,
        bool reducesStops) => new(
        true,
        targetConsumption,
        saving,
        reducesStops,
        strategy,
        $"FUEL SAVE {saving:P1} · {strategy.Stints} STINTS · {strategy.Stops} STOPS · " +
        (reducesStops ? "FEWER STOPS" : "SAME STOPS"),
        $"TARGET {targetConsumption:0.00} L/LAP · {strategy.PitPlan}",
        strategy.TirePlan);

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
