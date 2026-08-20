namespace LmuOverlay.Widgets;

// Compatibility facade retained for Desktop, SteamVR and third-party callers.
// All implementation lives in focused calculators beside this file.
public static class EnduranceStrategyPlanner
{
    public static EnduranceStrategyPlan Calculate(EnduranceStrategyInput input) =>
        FullPushStrategyCalculator.Calculate(input);

    public static FuelSaveStrategyPlan CalculateFuelSave(
        EnduranceStrategyInput input,
        EnduranceStrategyPlan fullPush,
        double currentFuelLiters,
        double maximumSavingFraction = 0.15) => FuelSaveStrategyCalculator.Calculate(
            input,
            fullPush,
            currentFuelLiters,
            maximumSavingFraction);
}
