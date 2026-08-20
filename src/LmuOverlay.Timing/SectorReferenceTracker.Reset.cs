using System.Diagnostics;
using LmuOverlay.Domain;

namespace LmuOverlay.Widgets;

public sealed partial class SectorReferenceTracker
{
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
