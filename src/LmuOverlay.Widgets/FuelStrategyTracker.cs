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
    string Status);

public sealed class FuelStrategyTracker
{
    private const int MaximumSamples = 5;
    private const double ReserveLaps = 1;

    private readonly Queue<double> _samples = new();
    private readonly Queue<double> _virtualEnergySamples = new();
    private string _trackName = string.Empty;
    private int _sessionCode = int.MinValue;
    private int _lastCompletedLaps = -1;
    private double _lapStartFuel;
    private double _previousFuel;
    private double _lapStartVirtualEnergy;
    private double _previousVirtualEnergy;

    public FuelStrategyWidgetState Update(LmuTelemetrySnapshot snapshot)
    {
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
        var average = _samples.Count > 0 ? _samples.Average() : 0;
        var virtualEnergyAverage = _virtualEnergySamples.Count > 0
            ? _virtualEnergySamples.Average()
            : 0;
        var lapsToFinish = EstimateLapsToFinish(session, playerStanding, completedLaps);
        var referenceLapSeconds = ReferenceLapSeconds(playerStanding);
        var required = average > 0
            ? average * (lapsToFinish + ReserveLaps)
            : 0;
        var margin = average > 0 ? player.FuelLiters - required : 0;
        var estimatedRange = average > 0 ? player.FuelLiters / average : 0;
        var virtualEnergyRange = virtualEnergyAverage > 0
            ? virtualEnergy / virtualEnergyAverage
            : 0;
        var requiredVirtualEnergy = virtualEnergyAverage > 0
            ? virtualEnergyAverage * (lapsToFinish + ReserveLaps)
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
            status);
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

    private static FuelStrategyWidgetState Unavailable() => new(
        false, true, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        "NO DATA");
}
