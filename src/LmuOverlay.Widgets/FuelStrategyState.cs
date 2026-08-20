using LmuOverlay.Domain;
using LmuOverlay.Strategy.Learning;
using LmuOverlay.Strategy.Planning;

namespace LmuOverlay.Widgets;

public sealed record FuelStrategyWidgetState(
    bool Available,
    bool Learning,
    double FuelLiters,
    double AverageConsumptionLitersPerLap,
    int Samples,
    double EstimatedRangeLaps,
    double EstimatedRangeTimeSeconds,
    int EstimatedLapsToFinish,
    double EstimatedTimeToFinishSeconds,
    double RequiredFuelLiters,
    double FuelMarginLiters,
    double VirtualEnergyFraction,
    double AverageVirtualEnergyFractionPerLap,
    double EstimatedVirtualEnergyRangeLaps,
    double EstimatedVirtualEnergyRangeTimeSeconds,
    double RequiredVirtualEnergyFraction,
    double VirtualEnergyMarginFraction,
    double ProjectedConsumptionLitersPerLap,
    double TargetConsumptionLitersPerLap,
    double RequiredFuelSavingFraction,
    int LapsUntilPit,
    int SuggestedPitLap,
    double FuelToAddLiters,
    string Confidence,
    string Status)
{
    public int EstimatedPitStops { get; init; }
    public double EstimatedTotalPitLossSeconds { get; init; }
    public string PlanSummary { get; init; } = string.Empty;
    public double AveragePaceSeconds { get; init; }
    public double PaceTrendSecondsPerLap { get; init; }
    public double CurrentMaximumTireWearFraction { get; init; }
    public double AverageTireWearFractionPerLap { get; init; }
    public double EstimatedStrategyTimeSeconds { get; init; }
    public int RecommendedTireSets { get; init; }
    public string PitPlan { get; init; } = string.Empty;
    public string TirePlan { get; init; } = string.Empty;
    public string AlternativePlan { get; init; } = string.Empty;
    public string FuelSavePlan { get; init; } = string.Empty;
    public string FuelSavePitPlan { get; init; } = string.Empty;
    public string FuelSaveTirePlan { get; init; } = string.Empty;
    public string FuelSaveLapPlan { get; init; } = string.Empty;
    public double FuelSaveTargetLitersPerLap { get; init; }
    public double FuelSaveFraction { get; init; }
    public bool FuelSaveReducesStopCount { get; init; }
    public double FinalFuelToAddLiters { get; init; }
    public double FinalVirtualEnergyTargetFraction { get; init; }
    public double FuelSaveVirtualEnergyTargetPerLap { get; init; }
    public string FlagScenario { get; init; } = string.Empty;
    public string WeatherScenario { get; init; } = string.Empty;
    public string TrafficScenario { get; init; } = string.Empty;
    public string ProbabilisticScenario { get; init; } = string.Empty;
    public double FinishProbability { get; init; }
    public double EffectiveFuelCapacityLiters { get; init; }
}

public sealed record FuelStrategyOptions(
    double FuelReserveLaps = 1,
    double EnergyReserveFraction = 0,
    int ManualRemainingLaps = 0,
    int MaximumStintLaps = 0,
    double EstimatedPitLossSeconds = 30,
    int AvailableTireSets = 0,
    double TireWearLimitFraction = 0.7,
    double EstimatedTireChangeSeconds = 15,
    double ManualRemainingMinutes = 0,
    double ManualLapTimeSeconds = 0,
    double ManualFuelPerLapLiters = 0,
    double ManualFuelCapacityLiters = 0);
