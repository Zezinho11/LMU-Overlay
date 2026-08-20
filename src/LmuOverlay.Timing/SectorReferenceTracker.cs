using System.Diagnostics;
using LmuOverlay.Domain;

namespace LmuOverlay.Widgets;

public sealed class SectorReferenceTracker
{
    private static readonly long ComparisonDurationTicks = Stopwatch.Frequency * 4;
    private readonly double[] _references = new double[3];
    private readonly double[] _persistentReferences = new double[3];
    private readonly double[] _lapCandidates = new double[3];
    private readonly double[] _currentLapSectors = new double[3];
    private readonly SectorReferenceOrigin[] _origins = new SectorReferenceOrigin[3];
    private readonly int[] _referenceLap = [-1, -1, -1];
    private readonly int[] _suppressReferenceOnLap = [-1, -1, -1];
    private readonly bool[] _contaminated = new bool[3];
    private string _sessionKey = string.Empty;
    private int _lastLapNumber = -1;
    private uint _lastScoringSequence;
    private int? _currentSector;
    private PendingSector? _pending;
    private double _lastLapElapsedSeconds;
    private double _lapSector1Seconds;
    private double _lapSector2Seconds;
    private bool _lapIsOutLap;
    private bool _lapInvalidated;
    private bool _hasSamples;
    private int _persistenceRevision;
    private PersonalBestLap _lastCompletedValidLap;
    private int _completedValidLapRevision;
    private PendingCompletedLap? _pendingCompletedLap;
    private int _recentSectorIndex = -1;
    private double _recentSectorTimeSeconds;
    private double _recentSectorReferenceSeconds;
    private long _recentSectorExpiresAtTimestamp;

    public int PersistenceRevision => _persistenceRevision;
    public int CompletedValidLapRevision => _completedValidLapRevision;
    public PersonalBestLap LastCompletedValidLap => _lastCompletedValidLap;

    public SectorReferenceSeed PersistentReferences => new(
        _persistentReferences[0],
        _persistentReferences[1],
        _persistentReferences[2]);

    public DashboardSectorTimes Update(
        LmuTelemetrySnapshot snapshot,
        DashboardSectorTimes observed,
        SectorReferenceSeed savedReferences = default)
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
            Reset(savedReferences);
            _sessionKey = sessionKey;
        }

        var standing = snapshot.Standings.FirstOrDefault(
            item => item.VehicleId == player.VehicleId || item.IsPlayer);
        var inPit = standing?.IsInPits == true ||
            standing?.PitState == LmuPitState.Entering ||
            standing?.PitState == LmuPitState.Stopped;
        var startsDuringPitExit = !_hasSamples &&
            standing?.PitState == LmuPitState.Exiting;
        if (!_hasSamples && (inPit || startsDuringPitExit || player.LapNumber <= 0))
        {
            _lapIsOutLap = true;
            _contaminated[NormalizeSector(player.CurrentSector)] = true;
        }

        var lapChanged = _lastLapNumber >= 0 && player.LapNumber != _lastLapNumber;
        if (player.LapInvalidated)
        {
            _lapInvalidated = true;
            Array.Clear(_currentLapSectors);
            RemoveInvalidatedProvisionalReferences(player.LapNumber);
        }
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
            var origin = _lapInvalidated || _lapIsOutLap
                ? SectorReferenceOrigin.None
                : SectorReferenceOrigin.Session;
            if (hasTelemetryClock && IsLapElapsedPlausible(transitionElapsedSeconds))
            {
                CaptureTelemetryTransition(
                    completed,
                    transitionElapsedSeconds,
                    origin,
                    player.LapNumber);
            }

            if (!_contaminated[completed] && origin != SectorReferenceOrigin.None)
            {
                _pending = new(
                    completed,
                    snapshot.ScoringSequence != _lastScoringSequence
                        ? 0
                        : Candidate(observed, completed),
                    snapshot.ScoringSequence,
                    origin,
                    player.LapNumber);
            }

            _contaminated[sector] = false;
            _currentSector = sector;
            if (sector == 1)
            {
                _lapSector1Seconds = 0;
                _lapSector2Seconds = 0;
            }
        }

        // A transition across start/finish completes the previous out lap.
        // From this point onward the new lap is eligible for clean references.
        if (lapChanged)
        {
            if (!_lapIsOutLap && !_lapInvalidated)
            {
                StageCompletedValidLap(
                    observed,
                    standing?.LastLapTimeSeconds ?? 0,
                    snapshot.ScoringSequence);
            }
            Array.Clear(_lapCandidates);
            Array.Clear(_currentLapSectors);
            _lapIsOutLap = false;
            _lapInvalidated = false;
        }
        if (inPit)
        {
            _lapIsOutLap = true;
            _contaminated[sector] = true;
        }

        CapturePending(snapshot.ScoringSequence, observed);
        CaptureCompletedValues(sector, inPit, observed, player.LapNumber);
        if (!_lapInvalidated)
        {
            CaptureLiveSectorClock(sector, lapElapsedSeconds);
        }
        CaptureOfficialBest(observed, player.LapNumber);
        ConfirmPendingCompletedLap(
            observed,
            standing?.LastLapTimeSeconds ?? 0,
            snapshot.ScoringSequence);
        _lastLapNumber = player.LapNumber;
        _lastScoringSequence = snapshot.ScoringSequence;
        _hasSamples = true;
        if (hasTelemetryClock)
        {
            _lastLapElapsedSeconds = lapElapsedSeconds;
        }

        return observed with
        {
            CurrentSector1Seconds = _currentLapSectors[0],
            CurrentSector2Seconds = _currentLapSectors[1],
            CurrentSector3Seconds = _currentLapSectors[2],
            BestSector1Seconds = ReferenceForLap(0, player.LapNumber),
            BestSector2Seconds = ReferenceForLap(1, player.LapNumber),
            BestSector3Seconds = ReferenceForLap(2, player.LapNumber),
            Sector1ReferenceOrigin = OriginForLap(0, player.LapNumber),
            Sector2ReferenceOrigin = OriginForLap(1, player.LapNumber),
            Sector3ReferenceOrigin = OriginForLap(2, player.LapNumber),
            RecentSectorIndex = Stopwatch.GetTimestamp() <= _recentSectorExpiresAtTimestamp
                ? _recentSectorIndex
                : -1,
            RecentSectorTimeSeconds = _recentSectorTimeSeconds,
            RecentSectorReferenceSeconds = _recentSectorReferenceSeconds,
            RecentSectorExpiresAtTimestamp = _recentSectorExpiresAtTimestamp,
        };
    }

    private void CaptureTelemetryTransition(
        int completedSector,
        double lapElapsedSeconds,
        SectorReferenceOrigin origin,
        int lapNumber)
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

        var referenceIndex = ReferenceIndexForSegment(completedSector);
        if (!_lapInvalidated && IsPlausible(segmentSeconds))
        {
            _currentLapSectors[referenceIndex] = segmentSeconds;
        }

        if (!_contaminated[completedSector])
        {
            RecordSectorCompletion(referenceIndex, segmentSeconds);
            CaptureSeed(
                referenceIndex,
                segmentSeconds,
                origin,
                lapNumber);
        }
    }

    private void CaptureLiveSectorClock(int activeSector, double lapElapsedSeconds)
    {
        if (!IsLapElapsedPlausible(lapElapsedSeconds))
        {
            return;
        }

        var referenceIndex = ReferenceIndexForSegment(activeSector);
        var elapsed = activeSector switch
        {
            1 => lapElapsedSeconds,
            2 when _lapSector1Seconds > 0 =>
                lapElapsedSeconds - _lapSector1Seconds,
            0 when _lapSector1Seconds > 0 && _lapSector2Seconds > 0 =>
                lapElapsedSeconds - _lapSector1Seconds - _lapSector2Seconds,
            _ => 0,
        };
        if (double.IsFinite(elapsed) && elapsed is >= 0 and < 600)
        {
            _currentLapSectors[referenceIndex] = elapsed;
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

        var referenceIndex = ReferenceIndexForSegment(pending.Sector);
        RecordSectorCompletion(referenceIndex, candidate);
        CaptureSeed(
            referenceIndex,
            candidate,
            pending.Origin,
            pending.LapNumber);
        _pending = null;
    }

    private void CaptureOfficialBest(DashboardSectorTimes observed, int lapNumber)
    {
        Capture(0, observed.BestSector1Seconds, lapNumber, persistOfficial: true);
        Capture(1, observed.BestSector2Seconds, lapNumber, persistOfficial: true);
        // LMU does not expose an independent best S3. The value derived from
        // best lap minus cumulative best S2 can mix different laps, so S3 is
        // accepted only from an exact sector transition or a saved reference.
    }

    private void CaptureCompletedValues(
        int activeSector,
        bool inPit,
        DashboardSectorTimes observed,
        int lapNumber)
    {
        var origin = _lapInvalidated || _lapIsOutLap
            ? SectorReferenceOrigin.None
            : SectorReferenceOrigin.Session;
        if (activeSector != 1 && !_contaminated[1])
        {
            CaptureSeed(0, observed.CurrentSector1Seconds, origin, lapNumber);
        }

        if (activeSector != 2 && !_contaminated[2])
        {
            CaptureSeed(1, observed.CurrentSector2Seconds, origin, lapNumber);
        }

        if (activeSector == 1 && !inPit)
        {
            CaptureSeed(2, observed.LastSector3Seconds, origin, lapNumber);
        }
    }

    private void Capture(
        int sector,
        double seconds,
        int lapNumber,
        bool persistOfficial)
    {
        if (!IsPlausible(seconds))
        {
            return;
        }

        var firstReference = _references[sector] <= 0;
        if (firstReference || seconds < _references[sector])
        {
            _references[sector] = seconds;
            _origins[sector] = SectorReferenceOrigin.Session;
            _referenceLap[sector] = -1;
            if (firstReference && _hasSamples)
            {
                _suppressReferenceOnLap[sector] = lapNumber;
            }
        }
        else if (Math.Abs(seconds - _references[sector]) <= 0.0005)
        {
            // The scoring stream has now confirmed a telemetry-derived
            // provisional value as an official valid sector.
            _origins[sector] = SectorReferenceOrigin.Session;
            _referenceLap[sector] = -1;
        }
        if (persistOfficial)
        {
            CapturePersistent(sector, seconds);
        }
    }

    private void CaptureSeed(
        int sector,
        double seconds,
        SectorReferenceOrigin origin,
        int lapNumber)
    {
        if (_references[sector] <= 0 && IsPlausible(seconds))
        {
            if (origin == SectorReferenceOrigin.None)
            {
                return;
            }
            _references[sector] = seconds;
            _origins[sector] = origin;
            _referenceLap[sector] = lapNumber;
            if (origin == SectorReferenceOrigin.Session && _hasSamples)
            {
                _suppressReferenceOnLap[sector] = lapNumber;
            }
        }

        if (origin == SectorReferenceOrigin.Session && IsPlausible(seconds))
        {
            _lapCandidates[sector] = _lapCandidates[sector] <= 0
                ? seconds
                : Math.Min(_lapCandidates[sector], seconds);
        }

    }

    private void StageCompletedValidLap(
        DashboardSectorTimes observed,
        double officialLastLapSeconds,
        uint scoringSequence)
    {
        _pendingCompletedLap = null;
        var telemetryLap = new PersonalBestLap(
            _lapCandidates.Sum(),
            _lapCandidates[0],
            _lapCandidates[1],
            _lapCandidates[2]);
        if (!telemetryLap.IsValid)
        {
            return;
        }

        var officialLap = OfficialCompletedLap(observed, officialLastLapSeconds);
        if (scoringSequence != _lastScoringSequence && officialLap.IsValid)
        {
            PublishCompletedValidLap(officialLap);
            return;
        }

        _pendingCompletedLap = new(
            telemetryLap,
            scoringSequence,
            officialLastLapSeconds,
            Stopwatch.GetTimestamp() + Stopwatch.Frequency * 2);
    }

    private void ConfirmPendingCompletedLap(
        DashboardSectorTimes observed,
        double officialLastLapSeconds,
        uint scoringSequence)
    {
        if (_pendingCompletedLap is not { } pending)
        {
            return;
        }

        if (scoringSequence != pending.ScoringSequence)
        {
            var official = OfficialCompletedLap(observed, officialLastLapSeconds);
            var changed = pending.LastLapAtStage <= 0 ||
                Math.Abs(officialLastLapSeconds - pending.LastLapAtStage) > 0.0005;
            if (changed && official.IsValid)
            {
                PublishCompletedValidLap(official);
                _pendingCompletedLap = null;
                return;
            }
        }

        if (Stopwatch.GetTimestamp() >= pending.FallbackAtTimestamp)
        {
            PublishCompletedValidLap(pending.TelemetryLap);
            _pendingCompletedLap = null;
        }
    }

    private void PublishCompletedValidLap(PersonalBestLap completed)
    {
        _lastCompletedValidLap = completed;
        _completedValidLapRevision++;
        for (var sector = 0; sector < 3; sector++)
        {
            CapturePersistent(sector, completed.Sectors[sector]);
        }
    }

    private static PersonalBestLap OfficialCompletedLap(
        DashboardSectorTimes observed,
        double officialLastLapSeconds) => new(
            officialLastLapSeconds,
            observed.LastSector1Seconds,
            observed.LastSector2Seconds,
            observed.LastSector3Seconds);

    private void RecordSectorCompletion(int sector, double seconds)
    {
        if (!IsPlausible(seconds) ||
            (_recentSectorIndex == sector &&
             Math.Abs(_recentSectorTimeSeconds - seconds) <= 0.0005 &&
             Stopwatch.GetTimestamp() <= _recentSectorExpiresAtTimestamp))
        {
            return;
        }

        _recentSectorIndex = sector;
        _recentSectorTimeSeconds = seconds;
        _recentSectorReferenceSeconds = _references[sector];
        _recentSectorExpiresAtTimestamp =
            Stopwatch.GetTimestamp() + ComparisonDurationTicks;
    }

    private void RemoveInvalidatedProvisionalReferences(int lapNumber)
    {
        for (var sector = 0; sector < 3; sector++)
        {
            if (_referenceLap[sector] != lapNumber ||
                _origins[sector] != SectorReferenceOrigin.Session)
            {
                continue;
            }

            _references[sector] = _persistentReferences[sector];
            _origins[sector] = _persistentReferences[sector] > 0
                ? SectorReferenceOrigin.Saved
                : SectorReferenceOrigin.None;
            _referenceLap[sector] = -1;
            _suppressReferenceOnLap[sector] = -1;
        }
    }

    private void CapturePersistent(int sector, double seconds)
    {
        if (!IsPlausible(seconds) ||
            (_persistentReferences[sector] > 0 &&
             seconds >= _persistentReferences[sector]))
        {
            return;
        }

        _persistentReferences[sector] = seconds;
        _persistenceRevision++;
    }

    private double ReferenceForLap(int sector, int lapNumber) =>
        _suppressReferenceOnLap[sector] == lapNumber
            ? 0
            : _references[sector];

    private SectorReferenceOrigin OriginForLap(int sector, int lapNumber) =>
        _suppressReferenceOnLap[sector] == lapNumber
            ? SectorReferenceOrigin.None
            : _origins[sector];

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

    // TelemInfoV01.mCurrentSector is zero-based (0=S1, 1=S2, 2=S3),
    // unlike VehicleScoringInfoV01.mSector (0=S3, 1=S1, 2=S2).
    // The telemetry value also stores pit-lane state in its sign bit.
    private static int NormalizeSector(int sector) => (sector & int.MaxValue) switch
    {
        0 => 1,
        1 => 2,
        2 => 0,
        _ => 1,
    };

    private static int ReferenceIndexForSegment(int sector) =>
        sector switch
        {
            1 => 0,
            2 => 1,
            _ => 2,
        };

    private void Reset(SectorReferenceSeed savedReferences = default)
    {
        Array.Clear(_references);
        Array.Clear(_persistentReferences);
        Array.Clear(_lapCandidates);
        Array.Clear(_currentLapSectors);
        Array.Clear(_origins);
        Array.Fill(_referenceLap, -1);
        Array.Fill(_suppressReferenceOnLap, -1);
        Array.Clear(_contaminated);
        for (var index = 0; index < 3; index++)
        {
            var saved = savedReferences[index];
            if (!IsPlausible(saved))
            {
                continue;
            }

            _references[index] = saved;
            _persistentReferences[index] = saved;
            _origins[index] = SectorReferenceOrigin.Saved;
        }
        _sessionKey = string.Empty;
        _lastLapNumber = -1;
        _lastScoringSequence = 0;
        _currentSector = null;
        _pending = null;
        _lastLapElapsedSeconds = 0;
        _lapSector1Seconds = 0;
        _lapSector2Seconds = 0;
        _lapIsOutLap = false;
        _lapInvalidated = false;
        _hasSamples = false;
        _persistenceRevision = 0;
        _lastCompletedValidLap = default;
        _completedValidLapRevision = 0;
        _pendingCompletedLap = null;
        _recentSectorIndex = -1;
        _recentSectorTimeSeconds = 0;
        _recentSectorReferenceSeconds = 0;
        _recentSectorExpiresAtTimestamp = 0;
    }

    private sealed record PendingSector(
        int Sector,
        double ValueAtTransition,
        uint ScoringSequence,
        SectorReferenceOrigin Origin,
        int LapNumber);

    private sealed record PendingCompletedLap(
        PersonalBestLap TelemetryLap,
        uint ScoringSequence,
        double LastLapAtStage,
        long FallbackAtTimestamp);
}
