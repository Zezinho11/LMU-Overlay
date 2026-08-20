using System.Diagnostics;
using LmuOverlay.Domain;

namespace LmuOverlay.Widgets;

public sealed partial class SectorReferenceTracker
{
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
}
