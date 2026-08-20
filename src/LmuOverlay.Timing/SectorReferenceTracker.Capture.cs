using System.Diagnostics;
using LmuOverlay.Domain;

namespace LmuOverlay.Widgets;

public sealed partial class SectorReferenceTracker
{
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
}
