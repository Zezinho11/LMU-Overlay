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
}

public sealed record FuelStrategyOptions(
    double FuelReserveLaps = 1,
    double EnergyReserveFraction = 0,
    int ManualRemainingLaps = 0,
    int MaximumStintLaps = 0,
    double EstimatedPitLossSeconds = 30);

public sealed class FuelStrategyTracker
{
    private const int MaximumSamples = 8;
    private readonly Queue<double> _samples = new();
    private readonly Queue<double> _virtualEnergySamples = new();
    private string _trackName = string.Empty;
    private int _sessionCode = int.MinValue;
    private int _lastCompletedLaps = -1;
    private double _lapStartFuel;
    private double _previousFuel;
    private double _lapStartVirtualEnergy;
    private double _previousVirtualEnergy;

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
            Reset(session, completedLaps, player.FuelLiters);
        }

        var refueled = player.FuelLiters > _previousFuel + 0.5;
        var virtualEnergy = NormalizeVirtualEnergy(player.VirtualEnergy);
        var virtualEnergyRefilled =
            virtualEnergy > _previousVirtualEnergy + 0.005;
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
            if (!refueled &&
                consumed > 0.05 &&
                consumed < Math.Max(1, player.FuelCapacityLiters))
            {
                _samples.Enqueue(consumed);
                while (_samples.Count > MaximumSamples)
                {
                    _samples.Dequeue();
                }
            }

            var virtualEnergyUsed = _lapStartVirtualEnergy - virtualEnergy;
            if (!virtualEnergyRefilled &&
                virtualEnergyUsed > 0.0001 &&
                virtualEnergyUsed <= 1)
            {
                _virtualEnergySamples.Enqueue(virtualEnergyUsed);
                while (_virtualEnergySamples.Count > MaximumSamples)
                {
                    _virtualEnergySamples.Dequeue();
                }
            }

            _lapStartFuel = player.FuelLiters;
            _lapStartVirtualEnergy = virtualEnergy;
            _lastCompletedLaps = completedLaps;
        }

        _previousFuel = player.FuelLiters;
        _previousVirtualEnergy = virtualEnergy;
        var average = WeightedAverage(_samples);
        var projectedConsumption = ConservativeProjection(_samples, average);
        var virtualEnergyAverage = _virtualEnergySamples.Count > 0
            ? WeightedAverage(_virtualEnergySamples)
            : 0;
        var lapsToFinish = options.ManualRemainingLaps > 0
            ? options.ManualRemainingLaps
            : EstimateLapsToFinish(session, playerStanding, completedLaps);
        var referenceLapSeconds = ReferenceLapSeconds(playerStanding);
        var required = projectedConsumption > 0
            ? projectedConsumption * (lapsToFinish + reserveLaps)
            : 0;
        var margin = projectedConsumption > 0 ? player.FuelLiters - required : 0;
        var estimatedRange = projectedConsumption > 0
            ? player.FuelLiters / projectedConsumption
            : 0;
        var targetConsumption = lapsToFinish >= 0
            ? player.FuelLiters / Math.Max(1, lapsToFinish + reserveLaps)
            : 0;
        var requiredSaving = projectedConsumption > targetConsumption &&
            projectedConsumption > 0
                ? 1 - (targetConsumption / projectedConsumption)
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
        var requiredVirtualEnergy = virtualEnergyAverage > 0
            ? virtualEnergyAverage * (lapsToFinish + reserveLaps) + energyReserve
            : 0;
        var virtualEnergyMargin = virtualEnergyAverage > 0
            ? virtualEnergy - requiredVirtualEnergy
            : 0;
        var virtualEnergyShort = virtualEnergyAverage > 0 &&
            virtualEnergyMargin < 0;
        var virtualEnergyMarginal = virtualEnergyAverage > 0 &&
            virtualEnergyMargin >= 0 &&
            virtualEnergyMargin < virtualEnergyAverage * 0.5;
        var status = _samples.Count == 0
            ? "LEARNING"
            : margin < 0 || virtualEnergyShort
                ? "SHORT"
                : margin < average * 0.5 || virtualEnergyMarginal
                    ? "MARGINAL"
                    : "GOOD";

        var fuelStintCapacity = projectedConsumption > 0 &&
            player.FuelCapacityLiters > 0
                ? Math.Max(1, (int)Math.Floor(
                    player.FuelCapacityLiters / projectedConsumption - reserveLaps))
                : int.MaxValue;
        var configuredStint = options.MaximumStintLaps > 0
            ? options.MaximumStintLaps
            : int.MaxValue;
        var effectiveStint = Math.Min(fuelStintCapacity, configuredStint);
        var estimatedPitStops = lapsToFinish > 0 && effectiveStint < int.MaxValue
            ? Math.Max(0, (int)Math.Ceiling(lapsToFinish / (double)effectiveStint) - 1)
            : 0;
        var pitLoss = estimatedPitStops *
            Math.Clamp(options.EstimatedPitLossSeconds, 0, 600);

        return new(
            true,
            _samples.Count == 0,
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
            Math.Max(0, required - player.FuelLiters),
            Confidence(_samples.Count),
            status)
        {
            EstimatedPitStops = estimatedPitStops,
            EstimatedTotalPitLossSeconds = pitLoss,
            PlanSummary = _samples.Count == 0
                ? "LEARNING STINT"
                : $"{estimatedPitStops} STOP{(estimatedPitStops == 1 ? string.Empty : "S")} · " +
                  $"PIT LOSS {pitLoss:0}s · RESERVE {reserveLaps:0.0}LAP",
        };
    }

    private bool HasSessionChanged(LmuSessionSnapshot session, int completedLaps) =>
        !string.Equals(_trackName, session.TrackName, StringComparison.Ordinal) ||
        _sessionCode != session.SessionCode ||
        completedLaps < _lastCompletedLaps;

    private void Reset(
        LmuSessionSnapshot session,
        int completedLaps,
        double fuelLiters)
    {
        _samples.Clear();
        _virtualEnergySamples.Clear();
        _trackName = session.TrackName;
        _sessionCode = session.SessionCode;
        _lastCompletedLaps = completedLaps;
        _lapStartFuel = fuelLiters;
        _previousFuel = fuelLiters;
        _lapStartVirtualEnergy = 0;
        _previousVirtualEnergy = 0;
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
