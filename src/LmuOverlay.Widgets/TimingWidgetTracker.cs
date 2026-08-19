using LmuOverlay.Domain;

namespace LmuOverlay.Widgets;

/// <summary>
/// Keeps timing identity and short-lived rival metadata coherent between
/// scoring updates. It never transfers data across a changed vehicle/driver
/// identity and bounds all prediction to one scoring interval.
/// </summary>
public sealed class TimingWidgetTracker
{
    private readonly Dictionary<int, VehicleCache> _vehicles = [];
    private string _sessionKey = string.Empty;
    private uint _scoringSequence;
    private DateTimeOffset _scoringCapturedAt;
    private double _lastSessionElapsed;

    public (LiveStandingsWidgetState Standings, RelativeWidgetState Relative) Update(
        LmuTelemetrySnapshot snapshot,
        int maximumRows,
        int carsEachSide)
    {
        var sessionKey = SessionKey(snapshot);
        var elapsed = snapshot.Session?.CurrentElapsedTime ?? 0;
        if (!string.Equals(sessionKey, _sessionKey, StringComparison.Ordinal) ||
            elapsed + 1 < _lastSessionElapsed)
        {
            _vehicles.Clear();
            _sessionKey = sessionKey;
            _scoringSequence = uint.MaxValue;
        }
        _lastSessionElapsed = elapsed;

        if (_scoringSequence != snapshot.ScoringSequence || _vehicles.Count == 0)
        {
            CaptureScoring(snapshot);
        }

        var coherent = CoherentSnapshot(snapshot);
        return (
            EssentialWidgetStateFactory.CreateLiveStandings(coherent, maximumRows),
            EssentialWidgetStateFactory.CreateRelative(coherent, carsEachSide));
    }

    private void CaptureScoring(LmuTelemetrySnapshot snapshot)
    {
        var lapLength = snapshot.Session?.LapLengthMeters ?? 0;
        var elapsed = _scoringCapturedAt == default
            ? 0
            : (snapshot.CapturedAt - _scoringCapturedAt).TotalSeconds;
        var seen = new HashSet<int>();
        foreach (var standing in snapshot.Standings)
        {
            seen.Add(standing.VehicleId);
            var identity = Identity(standing);
            var velocity = 0d;
            if (_vehicles.TryGetValue(standing.VehicleId, out var previous) &&
                previous.Identity == identity && elapsed is > 0.02 and < 5)
            {
                var lapDelta = standing.CompletedLaps - previous.Standing.CompletedLaps;
                var distance = standing.LapDistanceMeters - previous.Standing.LapDistanceMeters;
                if (lapLength > 100)
                {
                    distance += lapDelta * lapLength;
                }
                velocity = Math.Clamp(distance / elapsed, -10, 120);
            }

            _vehicles[standing.VehicleId] = new(
                identity,
                MergeMetadata(standing, previous, snapshot.CapturedAt),
                velocity,
                snapshot.CapturedAt);
        }

        foreach (var stale in _vehicles.Keys.Where(id => !seen.Contains(id)).ToArray())
        {
            _vehicles.Remove(stale);
        }
        _scoringSequence = snapshot.ScoringSequence;
        _scoringCapturedAt = snapshot.CapturedAt;
    }

    private LmuTelemetrySnapshot CoherentSnapshot(LmuTelemetrySnapshot snapshot)
    {
        var lapLength = snapshot.Session?.LapLengthMeters ?? 0;
        var projected = snapshot.Standings.Select(standing =>
        {
            if (!_vehicles.TryGetValue(standing.VehicleId, out var cache) ||
                cache.Identity != Identity(standing))
            {
                return standing;
            }

            var age = Math.Clamp((snapshot.CapturedAt - cache.CapturedAt).TotalSeconds, 0, 1);
            var distance = cache.Standing.LapDistanceMeters + cache.VelocityMetersPerSecond * age;
            if (lapLength > 100)
            {
                distance %= lapLength;
                if (distance < 0) distance += lapLength;
            }
            return cache.Standing with { LapDistanceMeters = distance };
        }).ToArray();
        return snapshot with { Standings = projected };
    }

    private static LmuVehicleStanding MergeMetadata(
        LmuVehicleStanding current,
        VehicleCache? previous,
        DateTimeOffset capturedAt)
    {
        if (previous is null || previous.Identity != Identity(current) ||
            capturedAt - previous.CapturedAt > TimeSpan.FromSeconds(2))
        {
            return current;
        }
        return current with
        {
            VehicleModel = string.IsNullOrWhiteSpace(current.VehicleModel)
                ? previous.Standing.VehicleModel
                : current.VehicleModel,
            FrontTireCompound = string.IsNullOrWhiteSpace(current.FrontTireCompound)
                ? previous.Standing.FrontTireCompound
                : current.FrontTireCompound,
            RearTireCompound = string.IsNullOrWhiteSpace(current.RearTireCompound)
                ? previous.Standing.RearTireCompound
                : current.RearTireCompound,
            VirtualEnergyFraction = current.VirtualEnergyFraction is >= 0 and <= 1
                ? current.VirtualEnergyFraction
                : previous.Standing.VirtualEnergyFraction,
        };
    }

    private static string Identity(LmuVehicleStanding standing) =>
        $"{standing.VehicleId}\u001f{standing.DriverName}\u001f{standing.VehicleName}";

    private static string SessionKey(LmuTelemetrySnapshot snapshot) => snapshot.Session is { } session
        ? $"{session.TrackName}\u001f{session.SessionCode}"
        : string.Empty;

    private sealed record VehicleCache(
        string Identity,
        LmuVehicleStanding Standing,
        double VelocityMetersPerSecond,
        DateTimeOffset CapturedAt);
}
