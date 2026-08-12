using LmuOverlay.Domain;

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
    public double FuelSaveTargetLitersPerLap { get; init; }
    public double FuelSaveFraction { get; init; }
    public bool FuelSaveReducesStopCount { get; init; }
    public double FinalFuelToAddLiters { get; init; }
    public double FinalVirtualEnergyTargetFraction { get; init; }
    public double FuelSaveVirtualEnergyTargetPerLap { get; init; }
    public string FlagScenario { get; init; } = string.Empty;
    public string WeatherScenario { get; init; } = string.Empty;
    public string TrafficScenario { get; init; } = string.Empty;
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

public sealed class FuelStrategyTracker
{
    private const int MaximumSamples = 8;
    private readonly Queue<double> _samples = new();
    private readonly Queue<double> _virtualEnergySamples = new();
    private readonly Queue<double> _paceSamples = new();
    private readonly Queue<double> _tireWearSamples = new();
    private readonly Queue<double> _rainSamples = new();
    private string _trackName = string.Empty;
    private int _sessionCode = int.MinValue;
    private int _lastCompletedLaps = -1;
    private double _lapStartFuel;
    private double _previousFuel;
    private double _lapStartVirtualEnergy;
    private double _previousVirtualEnergy;
    private double _previousMaximumTireWear;
    private DateTimeOffset _lastRainSampleAt = DateTimeOffset.MinValue;
    private DateTimeOffset _previousSampleAt = DateTimeOffset.MinValue;
    private bool _lapContaminated;

    public FuelStrategyWidgetState Update(
        LmuTelemetrySnapshot snapshot,
        FuelStrategyOptions? options = null)
    {
        options ??= new();
        var reserveLaps = Math.Clamp(options.FuelReserveLaps, 0, 5);
        var energyReserve = Math.Clamp(options.EnergyReserveFraction, 0, 0.25);
        if (snapshot.Session is not { } session ||
            snapshot.Player is not { } player)
        {
            return Unavailable();
        }

        var playerStanding = snapshot.Standings.FirstOrDefault(item => item.IsPlayer);
        var completedLaps = playerStanding?.CompletedLaps
            ?? Math.Max(0, player.LapNumber - 1);
        if (HasSessionChanged(session, completedLaps))
        {
            Reset(
                session,
                completedLaps,
                player.FuelLiters,
                NormalizeVirtualEnergy(player.VirtualEnergy),
                MaximumTireWear(player.TireWear));
        }

        CaptureRainSample(session.Weather.RainIntensity, snapshot.CapturedAt);

        var refueled = player.FuelLiters > _previousFuel + 0.5;
        var virtualEnergy = NormalizeVirtualEnergy(player.VirtualEnergy);
        var virtualEnergyRefilled =
            virtualEnergy > _previousVirtualEnergy + 0.005;
        var inPit = IsInPitLane(playerStanding);
        var initialOutLap = completedLaps == 0 || player.LapNumber <= 0;
        var elapsedSincePrevious = _previousSampleAt == DateTimeOffset.MinValue
            ? TimeSpan.MaxValue
            : snapshot.CapturedAt - _previousSampleAt;
        var abruptFuelChange = elapsedSincePrevious >= TimeSpan.Zero &&
            elapsedSincePrevious < TimeSpan.FromSeconds(2) &&
            Math.Abs(player.FuelLiters - _previousFuel) > 5;
        if (inPit || initialOutLap || refueled || virtualEnergyRefilled || abruptFuelChange)
        {
            _lapContaminated = true;
        }
        if (refueled)
        {
            _lapStartFuel = player.FuelLiters;
        }

        if (virtualEnergyRefilled)
        {
            _lapStartVirtualEnergy = virtualEnergy;
        }

        if (completedLaps > _lastCompletedLaps)
        {
            var consumed = _lapStartFuel - player.FuelLiters;
            if (!_lapContaminated &&
                IsPlausibleFuelSample(consumed, player.FuelCapacityLiters, _samples))
            {
                EnqueueSample(_samples, consumed);
            }

            var virtualEnergyUsed = _lapStartVirtualEnergy - virtualEnergy;
            if (!_lapContaminated &&
                IsPlausibleEnergySample(virtualEnergyUsed, _virtualEnergySamples))
            {
                EnqueueSample(_virtualEnergySamples, virtualEnergyUsed);
            }

            if (!_lapContaminated &&
                playerStanding?.LastLapTimeSeconds is > 10 and < 1800)
            {
                EnqueueSample(_paceSamples, playerStanding.LastLapTimeSeconds);
            }

            var maximumTireWear = MaximumTireWear(player.TireWear);
            var tireWearUsed = maximumTireWear - _previousMaximumTireWear;
            if (!_lapContaminated &&
                tireWearUsed > 0.00001 && tireWearUsed < 0.25)
            {
                EnqueueSample(_tireWearSamples, tireWearUsed);
            }

            _previousMaximumTireWear = maximumTireWear;

            _lapStartFuel = player.FuelLiters;
            _lapStartVirtualEnergy = virtualEnergy;
            _lastCompletedLaps = completedLaps;
            _lapContaminated = inPit || refueled || virtualEnergyRefilled;
        }

        _previousFuel = player.FuelLiters;
        _previousVirtualEnergy = virtualEnergy;
        _previousSampleAt = snapshot.CapturedAt;
        var learnedAverage = WeightedAverage(_samples);
        var manualConsumption = Math.Clamp(
            options.ManualFuelPerLapLiters,
            0,
            100);
        var average = manualConsumption > 0
            ? manualConsumption
            : learnedAverage;
        var projectedConsumption = manualConsumption > 0
            ? manualConsumption
            : ConservativeProjection(_samples, average);
        var virtualEnergyAverage = _virtualEnergySamples.Count > 0
            ? WeightedAverage(_virtualEnergySamples)
            : 0;
        var manualLapTime = Math.Clamp(options.ManualLapTimeSeconds, 0, 3600);
        var averagePace = manualLapTime > 0
            ? manualLapTime
            : _paceSamples.Count > 0
                ? WeightedAverage(_paceSamples)
                : ReferenceLapSeconds(playerStanding);
        var paceTrend = Math.Clamp(LinearTrend(_paceSamples), 0, 5);
        var maximumWear = MaximumTireWear(player.TireWear);
        var tireWearPerLap = _tireWearSamples.Count >= 3
            ? ConservativeProjection(
                _tireWearSamples,
                WeightedAverage(_tireWearSamples))
            : 0;
        var referenceLapSeconds = manualLapTime > 0
            ? manualLapTime
            : ReferenceLapSeconds(playerStanding);
        var manualRemainingSeconds = Math.Clamp(
            options.ManualRemainingMinutes,
            0,
            24 * 60) * 60;
        var fixedDurationSeconds = options.ManualRemainingLaps > 0
            ? 0
            : manualRemainingSeconds > 0
                ? manualRemainingSeconds
                : LmuSessionLimits.HasFiniteLapLimit(session.MaximumLaps)
                    ? 0
                    : Math.Max(0, session.EndElapsedTime - session.CurrentElapsedTime);
        var lapsToFinish = options.ManualRemainingLaps > 0
            ? options.ManualRemainingLaps
            : manualRemainingSeconds > 0 && referenceLapSeconds > 0
                ? (int)Math.Ceiling(manualRemainingSeconds / referenceLapSeconds)
                : EstimateLapsToFinish(session, playerStanding, completedLaps);
        var required = projectedConsumption > 0
            ? projectedConsumption * (lapsToFinish + reserveLaps)
            : 0;
        var estimatedRange = projectedConsumption > 0
            ? player.FuelLiters / projectedConsumption
            : 0;
        var fuelLapsUntilPit = projectedConsumption > 0
            ? Math.Max(
                0,
                (int)Math.Floor(
                    player.FuelLiters / projectedConsumption - reserveLaps))
            : 0;
        var virtualEnergyRange = virtualEnergyAverage > 0
            ? virtualEnergy / virtualEnergyAverage
            : 0;
        var energyLapsUntilPit = virtualEnergyAverage > 0
            ? Math.Max(
                0,
                (int)Math.Floor(
                    virtualEnergy / virtualEnergyAverage -
                    Math.Max(reserveLaps, energyReserve / virtualEnergyAverage)))
            : int.MaxValue;
        var lapsUntilPit = projectedConsumption > 0
            ? Math.Min(fuelLapsUntilPit, energyLapsUntilPit)
            : 0;
        var suggestedPitLap = projectedConsumption > 0
            ? completedLaps + lapsUntilPit
            : 0;
        var fuelCapacity = options.ManualFuelCapacityLiters > 0
            ? Math.Clamp(options.ManualFuelCapacityLiters, 1, 1000)
            : player.FuelCapacityLiters;
        var fuelStintCapacity = projectedConsumption > 0 &&
            fuelCapacity > 0
                ? Math.Max(1, (int)Math.Floor(
                    fuelCapacity / projectedConsumption))
                : int.MaxValue;
        var strategyInput = new EnduranceStrategyInput(
            completedLaps,
            lapsToFinish,
            Math.Max(1, fuelLapsUntilPit),
            fuelStintCapacity,
            options.MaximumStintLaps,
            averagePace,
            0,
            projectedConsumption,
            fuelCapacity,
            projectedConsumption * reserveLaps,
            Math.Clamp(options.EstimatedPitLossSeconds, 0, 600),
            Math.Clamp(options.EstimatedTireChangeSeconds, 0, 180),
            maximumWear,
            tireWearPerLap,
            Math.Clamp(options.TireWearLimitFraction, 0.2, 0.95),
            Math.Max(0, options.AvailableTireSets))
        {
            FixedDurationSeconds = fixedDurationSeconds,
        };
        var strategy = EnduranceStrategyPlanner.Calculate(strategyInput);
        var fuelSave = EnduranceStrategyPlanner.CalculateFuelSave(
            strategyInput,
            strategy,
            player.FuelLiters);
        var hasPlannedStop = strategy.Available && strategy.Stops > 0;
        var currentStintTargetLaps = hasPlannedStop
            ? strategy.StintLaps[0]
            : lapsToFinish;
        var currentStintRequired = projectedConsumption > 0
            ? projectedConsumption * (currentStintTargetLaps + reserveLaps)
            : 0;
        var margin = projectedConsumption > 0
            ? player.FuelLiters - currentStintRequired
            : 0;
        var targetConsumption = currentStintTargetLaps >= 0
            ? player.FuelLiters /
              Math.Max(1, currentStintTargetLaps + reserveLaps)
            : 0;
        var requiredSaving = projectedConsumption > targetConsumption &&
            projectedConsumption > 0
                ? 1 - (targetConsumption / projectedConsumption)
                : 0;
        var requiredVirtualEnergy = virtualEnergyAverage > 0
            ? virtualEnergyAverage *
              (currentStintTargetLaps + reserveLaps) + energyReserve
            : 0;
        var virtualEnergyMargin = virtualEnergyAverage > 0
            ? virtualEnergy - requiredVirtualEnergy
            : 0;
        var virtualEnergyShort = virtualEnergyAverage > 0 &&
            virtualEnergyMargin < 0;
        var virtualEnergyMarginal = virtualEnergyAverage > 0 &&
            virtualEnergyMargin >= 0 &&
            virtualEnergyMargin < virtualEnergyAverage * 0.5;
        var isLearning = _samples.Count == 0 && manualConsumption <= 0;
        var status = isLearning
            ? "LEARNING"
            : margin < 0 || virtualEnergyShort
                ? "SHORT"
                : margin < average * 0.5 || virtualEnergyMarginal
                    ? "MARGINAL"
                    : "GOOD";
        var scenarioAdvice = RaceScenarioAdvisor.Calculate(
            strategyInput,
            strategy,
            new(
                session.GamePhase,
                session.Weather.RainIntensity,
                session.Weather.AveragePathWetness,
                LinearTrend(_rainSamples),
                player.GapToCarAheadSeconds,
                player.GapToCarBehindSeconds,
                completedLaps,
                lapsUntilPit,
                suggestedPitLap,
                maximumWear,
                Math.Clamp(options.TireWearLimitFraction, 0.2, 0.95),
                FormatTireCompound(player)));
        var estimatedPitStops = strategy.Available ? strategy.Stops : 0;
        var pitLoss = estimatedPitStops *
            Math.Clamp(options.EstimatedPitLossSeconds, 0, 600);

        return new(
            true,
            isLearning,
            player.FuelLiters,
            average,
            _samples.Count,
            estimatedRange,
            estimatedRange * referenceLapSeconds,
            lapsToFinish,
            lapsToFinish * referenceLapSeconds,
            required,
            margin,
            virtualEnergy,
            virtualEnergyAverage,
            virtualEnergyRange,
            virtualEnergyRange * referenceLapSeconds,
            requiredVirtualEnergy,
            virtualEnergyMargin,
            projectedConsumption,
            targetConsumption,
            requiredSaving,
            lapsUntilPit,
            suggestedPitLap,
            hasPlannedStop && strategy.FuelAtStopsLiters.Count > 0
                ? strategy.FuelAtStopsLiters[0]
                : Math.Max(0, required - player.FuelLiters),
            manualConsumption > 0 ? "MANUAL" : Confidence(_samples.Count),
            status)
        {
            EstimatedPitStops = estimatedPitStops,
            EstimatedTotalPitLossSeconds = pitLoss,
            PlanSummary = isLearning || !strategy.Available
                ? "LEARNING STINT"
                : strategy.Summary,
            AveragePaceSeconds = averagePace,
            PaceTrendSecondsPerLap = paceTrend,
            CurrentMaximumTireWearFraction = maximumWear,
            AverageTireWearFractionPerLap = tireWearPerLap,
            EstimatedStrategyTimeSeconds = strategy.EstimatedRaceTimeSeconds,
            RecommendedTireSets = strategy.TireSets,
            PitPlan = strategy.PitPlan,
            TirePlan = strategy.TirePlan,
            AlternativePlan = strategy.AlternativeSummary,
            FuelSavePlan = fuelSave.Summary,
            FuelSavePitPlan = fuelSave.PitPlan,
            FuelSaveTirePlan = fuelSave.TirePlan,
            FuelSaveTargetLitersPerLap = fuelSave.TargetConsumptionLitersPerLap,
            FuelSaveFraction = fuelSave.SavingFraction,
            FuelSaveReducesStopCount = fuelSave.ReducesStopCount,
            FinalFuelToAddLiters = strategy.FuelAtStopsLiters.LastOrDefault(),
            FinalVirtualEnergyTargetFraction = strategy.StintLaps.Count > 0 &&
                virtualEnergyAverage > 0
                    ? Math.Min(
                        1,
                        virtualEnergyAverage *
                        (strategy.StintLaps[^1] + reserveLaps) + energyReserve)
                    : 0,
            FuelSaveVirtualEnergyTargetPerLap = virtualEnergyAverage > 0 && fuelSave.Available
                ? virtualEnergyAverage * (1 - fuelSave.SavingFraction)
                : 0,
            FlagScenario = scenarioAdvice.FlagState,
            WeatherScenario = scenarioAdvice.Weather,
            TrafficScenario = scenarioAdvice.Traffic,
        };
    }

    private bool HasSessionChanged(LmuSessionSnapshot session, int completedLaps) =>
        !string.Equals(_trackName, session.TrackName, StringComparison.Ordinal) ||
        _sessionCode != session.SessionCode ||
        completedLaps < _lastCompletedLaps;

    private void Reset(
        LmuSessionSnapshot session,
        int completedLaps,
        double fuelLiters,
        double virtualEnergy,
        double maximumTireWear)
    {
        _samples.Clear();
        _virtualEnergySamples.Clear();
        _paceSamples.Clear();
        _tireWearSamples.Clear();
        _rainSamples.Clear();
        _trackName = session.TrackName;
        _sessionCode = session.SessionCode;
        _lastCompletedLaps = completedLaps;
        _lapStartFuel = fuelLiters;
        _previousFuel = fuelLiters;
        _lapStartVirtualEnergy = virtualEnergy;
        _previousVirtualEnergy = virtualEnergy;
        _previousMaximumTireWear = maximumTireWear;
        _lastRainSampleAt = DateTimeOffset.MinValue;
        _previousSampleAt = DateTimeOffset.MinValue;
        _lapContaminated = completedLaps == 0;
    }

    private static bool IsInPitLane(LmuVehicleStanding? standing) =>
        standing?.IsInPits == true ||
        standing?.PitState == LmuPitState.Entering ||
        standing?.PitState == LmuPitState.Stopped;

    private static bool IsPlausibleFuelSample(
        double consumed,
        double capacity,
        IEnumerable<double> samples)
    {
        var maximum = capacity > 0
            ? Math.Clamp(capacity * 0.25, 5, 20)
            : 20;
        if (!double.IsFinite(consumed) || consumed is <= 0.05 || consumed > maximum)
        {
            return false;
        }

        return IsConsistentWithHistory(consumed, samples);
    }

    private static bool IsPlausibleEnergySample(
        double consumed,
        IEnumerable<double> samples) =>
        double.IsFinite(consumed) &&
        consumed is > 0.0001 and <= 1 &&
        IsConsistentWithHistory(consumed, samples);

    private static bool IsConsistentWithHistory(
        double value,
        IEnumerable<double> samples)
    {
        var history = samples.OrderBy(sample => sample).ToArray();
        if (history.Length < 2)
        {
            return true;
        }

        var median = history.Length % 2 == 0
            ? (history[history.Length / 2 - 1] + history[history.Length / 2]) / 2
            : history[history.Length / 2];
        return median <= 0 || value >= median * 0.45 && value <= median * 1.8;
    }

    private void CaptureRainSample(double rainIntensity, DateTimeOffset capturedAt)
    {
        if (_lastRainSampleAt != DateTimeOffset.MinValue &&
            capturedAt - _lastRainSampleAt < TimeSpan.FromSeconds(1))
        {
            return;
        }

        _rainSamples.Enqueue(Math.Clamp(rainIntensity, 0, 1));
        while (_rainSamples.Count > 20)
        {
            _rainSamples.Dequeue();
        }

        _lastRainSampleAt = capturedAt;
    }

    private static string FormatTireCompound(LmuPlayerTelemetry player)
    {
        var front = player.FrontTireCompound.Trim();
        var rear = player.RearTireCompound.Trim();
        if (front.Length == 0 && rear.Length == 0)
        {
            return "UNKNOWN";
        }

        return string.Equals(front, rear, StringComparison.OrdinalIgnoreCase) ||
               rear.Length == 0
            ? front
            : front.Length == 0
                ? rear
                : $"{front}/{rear}";
    }

    private static void EnqueueSample(Queue<double> samples, double value)
    {
        samples.Enqueue(value);
        while (samples.Count > MaximumSamples)
        {
            samples.Dequeue();
        }
    }

    private static double MaximumTireWear(LmuWheelWear wear) => Math.Max(
        Math.Max(wear.FrontLeftFraction, wear.FrontRightFraction),
        Math.Max(wear.RearLeftFraction, wear.RearRightFraction));

    private static double LinearTrend(IEnumerable<double> samples)
    {
        var values = samples.ToArray();
        if (values.Length < 3)
        {
            return 0;
        }

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

    private static double NormalizeVirtualEnergy(double value) =>
        double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;

    private static int EstimateLapsToFinish(
        LmuSessionSnapshot session,
        LmuVehicleStanding? playerStanding,
        int completedLaps)
    {
        if (LmuSessionLimits.HasFiniteLapLimit(session.MaximumLaps))
        {
            return Math.Max(0, session.MaximumLaps - completedLaps);
        }

        var remainingSeconds = Math.Max(
            0,
            session.EndElapsedTime - session.CurrentElapsedTime);
        var referenceLapSeconds = playerStanding?.LastLapTimeSeconds > 0
            ? playerStanding.LastLapTimeSeconds
            : playerStanding?.BestLapTimeSeconds ?? 0;
        return remainingSeconds > 0 && referenceLapSeconds > 0
            ? (int)Math.Ceiling(remainingSeconds / referenceLapSeconds)
            : 0;
    }

    private static double ReferenceLapSeconds(LmuVehicleStanding? standing) =>
        standing?.LastLapTimeSeconds > 0
            ? standing.LastLapTimeSeconds
            : standing?.BestLapTimeSeconds > 0
                ? standing.BestLapTimeSeconds
                : 0;

    private static double WeightedAverage(IEnumerable<double> samples)
    {
        var values = samples.ToArray();
        if (values.Length == 0)
        {
            return 0;
        }

        double weightedTotal = 0;
        double totalWeight = 0;
        for (var index = 0; index < values.Length; index++)
        {
            var weight = index + 1;
            weightedTotal += values[index] * weight;
            totalWeight += weight;
        }

        return weightedTotal / totalWeight;
    }

    private static double ConservativeProjection(
        IEnumerable<double> samples,
        double weightedAverage)
    {
        var values = samples.ToArray();
        if (values.Length < 2)
        {
            return weightedAverage;
        }

        var variance = values.Sum(
            value => Math.Pow(value - weightedAverage, 2)) / values.Length;
        return weightedAverage + Math.Sqrt(variance) * 0.35;
    }

    private static string Confidence(int samples) => samples switch
    {
        >= 6 => "HIGH",
        >= 3 => "MEDIUM",
        > 0 => "LOW",
        _ => "LEARNING",
    };

    private static FuelStrategyWidgetState Unavailable() => new(
        false, true, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, "LEARNING",
        "NO DATA");
}
