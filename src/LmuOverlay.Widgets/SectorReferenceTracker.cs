using LmuOverlay.Domain;

namespace LmuOverlay.Widgets;

public sealed class SectorReferenceTracker
{
    private readonly double[] _references = new double[3];
    private readonly bool[] _contaminated = new bool[3];
    private string _sessionKey = string.Empty;
    private int _lastLapNumber = -1;
    private uint _lastScoringSequence;
    private int? _currentSector;
    private PendingSector? _pending;
    private double _lastLapElapsedSeconds;
    private double _lapSector1Seconds;
    private double _lapSector2Seconds;

    public DashboardSectorTimes Update(
        LmuTelemetrySnapshot snapshot,
        DashboardSectorTimes observed)
    {
        if (snapshot.Player is not { } player || snapshot.Session is not { } session)
        {
            Reset();
            return observed;
        }

        var sessionKey = $"{session.TrackName}\u001f{session.SessionCode}\u001f{player.VehicleId}";
        if (!string.Equals(_sessionKey, sessionKey, StringComparison.Ordinal) ||
            (_lastLapNumber >= 0 && player.LapNumber < _lastLapNumber))
        {
            Reset();
            _sessionKey = sessionKey;
        }

        var standing = snapshot.Standings.FirstOrDefault(
            item => item.VehicleId == player.VehicleId || item.IsPlayer);
        var inPit = standing?.IsInPits == true ||
            standing?.PitState == LmuPitState.Entering ||
            standing?.PitState == LmuPitState.Stopped ||
            standing?.PitState == LmuPitState.Exiting;
        var sector = NormalizeSector(player.CurrentSector);
        var lapElapsedSeconds = player.ElapsedTime - player.LapStartElapsedTime;
        var hasTelemetryClock = IsLapElapsedPlausible(lapElapsedSeconds);
        if (_currentSector is null)
        {
            _currentSector = sector;
        }
        else if (_currentSector.Value != sector)
        {
            var completed = _currentSector.Value;
            var transitionElapsedSeconds = completed == 0
                ? _lastLapElapsedSeconds
                : lapElapsedSeconds;
            if (hasTelemetryClock && IsLapElapsedPlausible(transitionElapsedSeconds))
            {
                CaptureTelemetryTransition(completed, transitionElapsedSeconds);
            }

            if (!_contaminated[completed])
            {
                _pending = new(
                    completed,
                    snapshot.ScoringSequence != _lastScoringSequence
                        ? 0
                        : Candidate(observed, completed),
                    snapshot.ScoringSequence);
            }

            _contaminated[sector] = false;
            _currentSector = sector;
            if (sector == 1)
            {
                _lapSector1Seconds = 0;
                _lapSector2Seconds = 0;
            }
        }

        if (inPit)
        {
            _contaminated[sector] = true;
        }

        CapturePending(snapshot.ScoringSequence, observed);
        CaptureCompletedValues(sector, inPit, observed);
        CaptureOfficialBest(observed);
        _lastLapNumber = player.LapNumber;
        _lastScoringSequence = snapshot.ScoringSequence;
        if (hasTelemetryClock)
        {
            _lastLapElapsedSeconds = lapElapsedSeconds;
        }

        return observed with
        {
            BestSector1Seconds = ReferenceOrObserved(0, observed.BestSector1Seconds),
            BestSector2Seconds = ReferenceOrObserved(1, observed.BestSector2Seconds),
            BestSector3Seconds = ReferenceOrObserved(2, observed.BestSector3Seconds),
        };
    }

    private void CaptureTelemetryTransition(int completedSector, double lapElapsedSeconds)
    {
        double segmentSeconds;
        switch (completedSector)
        {
            case 1:
                _lapSector1Seconds = lapElapsedSeconds;
                segmentSeconds = lapElapsedSeconds;
                break;
            case 2:
                segmentSeconds = lapElapsedSeconds - _lapSector1Seconds;
                _lapSector2Seconds = segmentSeconds;
                break;
            default:
                segmentSeconds = lapElapsedSeconds -
                    _lapSector1Seconds -
                    _lapSector2Seconds;
                break;
        }

        if (!_contaminated[completedSector])
        {
            CaptureSeed(ReferenceIndexForSegment(completedSector), segmentSeconds);
        }
    }

    private void CapturePending(uint scoringSequence, DashboardSectorTimes observed)
    {
        if (_pending is not { } pending)
        {
            return;
        }

        var candidate = Candidate(observed, pending.Sector);
        var scoringAdvanced = scoringSequence != pending.ScoringSequence;
        var valueChanged = Math.Abs(candidate - pending.ValueAtTransition) > 0.0005;
        if (!IsPlausible(candidate) ||
            (!scoringAdvanced && !valueChanged && pending.ValueAtTransition > 0))
        {
            return;
        }

        CaptureSeed(ReferenceIndexForSegment(pending.Sector), candidate);
        _pending = null;
    }

    private void CaptureOfficialBest(DashboardSectorTimes observed)
    {
        Capture(0, observed.BestSector1Seconds);
        Capture(1, observed.BestSector2Seconds);
        Capture(2, observed.BestSector3Seconds);
    }

    private void CaptureCompletedValues(
        int activeSector,
        bool inPit,
        DashboardSectorTimes observed)
    {
        if (activeSector != 1 && !_contaminated[1])
        {
            CaptureSeed(0, observed.CurrentSector1Seconds);
        }

        if (activeSector != 2 && !_contaminated[2])
        {
            CaptureSeed(1, observed.CurrentSector2Seconds);
        }

        if (activeSector == 1 && !inPit)
        {
            CaptureSeed(2, observed.LastSector3Seconds);
        }
    }

    private void Capture(int sector, double seconds)
    {
        if (!IsPlausible(seconds))
        {
            return;
        }

        _references[sector] = _references[sector] <= 0
            ? seconds
            : Math.Min(_references[sector], seconds);
    }

    private void CaptureSeed(int sector, double seconds)
    {
        if (_references[sector] <= 0 && IsPlausible(seconds))
        {
            _references[sector] = seconds;
        }
    }

    private double ReferenceOrObserved(int sector, double observed) =>
        _references[sector] > 0
            ? observed > 0
                ? Math.Min(_references[sector], observed)
                : _references[sector]
            : observed;

    private static double Candidate(DashboardSectorTimes observed, int sector) =>
        sector switch
        {
            1 => observed.CurrentSector1Seconds,
            2 => observed.CurrentSector2Seconds,
            _ => observed.LastSector3Seconds,
        };

    private static bool IsPlausible(double seconds) =>
        double.IsFinite(seconds) && seconds is > 1 and < 600;

    private static bool IsLapElapsedPlausible(double seconds) =>
        double.IsFinite(seconds) && seconds is >= 0 and < 1_800;

    private static int NormalizeSector(int sector) => sector is 1 or 2 ? sector : 0;

    private static int ReferenceIndexForSegment(int sector) =>
        sector switch
        {
            1 => 0,
            2 => 1,
            _ => 2,
        };

    private void Reset()
    {
        Array.Clear(_references);
        Array.Clear(_contaminated);
        _sessionKey = string.Empty;
        _lastLapNumber = -1;
        _lastScoringSequence = 0;
        _currentSector = null;
        _pending = null;
        _lastLapElapsedSeconds = 0;
        _lapSector1Seconds = 0;
        _lapSector2Seconds = 0;
    }

    private sealed record PendingSector(
        int Sector,
        double ValueAtTransition,
        uint ScoringSequence);
}
