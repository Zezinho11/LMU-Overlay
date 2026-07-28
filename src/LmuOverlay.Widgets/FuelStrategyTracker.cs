using LmuOverlay.Domain;

namespace LmuOverlay.Widgets;

public sealed record FuelStrategyWidgetState(
    bool Available,
    bool Learning,
    double FuelLiters,
    double AverageConsumptionLitersPerLap,
    int Samples,
    double EstimatedRangeLaps,
    int EstimatedLapsToFinish,
    double RequiredFuelLiters,
    double FuelMarginLiters,
    string Status);

public sealed class FuelStrategyTracker
{
    private const int MaximumSamples = 5;
    private const double ReserveLaps = 1;

    private readonly Queue<double> _samples = new();
    private string _trackName = string.Empty;
    private int _sessionCode = int.MinValue;
    private int _lastCompletedLaps = -1;
    private double _lapStartFuel;
    private double _previousFuel;

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
        if (refueled)
        {
            _lapStartFuel = player.FuelLiters;
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

            _lapStartFuel = player.FuelLiters;
            _lastCompletedLaps = completedLaps;
        }

        _previousFuel = player.FuelLiters;
        var average = _samples.Count > 0 ? _samples.Average() : 0;
        var lapsToFinish = EstimateLapsToFinish(session, playerStanding, completedLaps);
        var required = average > 0
            ? average * (lapsToFinish + ReserveLaps)
            : 0;
        var margin = average > 0 ? player.FuelLiters - required : 0;
        var status = _samples.Count == 0
            ? "LEARNING"
            : margin < 0
                ? "SHORT"
                : margin < average * 0.5
                    ? "MARGINAL"
                    : "GOOD";

        return new(
            true,
            _samples.Count == 0,
            player.FuelLiters,
            average,
            _samples.Count,
            average > 0 ? player.FuelLiters / average : 0,
            lapsToFinish,
            required,
            margin,
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
        _trackName = session.TrackName;
        _sessionCode = session.SessionCode;
        _lastCompletedLaps = completedLaps;
        _lapStartFuel = fuelLiters;
        _previousFuel = fuelLiters;
    }

    private static int EstimateLapsToFinish(
        LmuSessionSnapshot session,
        LmuVehicleStanding? playerStanding,
        int completedLaps)
    {
        if (session.MaximumLaps > 0)
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

    private static FuelStrategyWidgetState Unavailable() => new(
        false, true, 0, 0, 0, 0, 0, 0, 0, "NO DATA");
}
