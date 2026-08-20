using System.Diagnostics;
using LmuOverlay.Domain;

namespace LmuOverlay.Widgets;

public sealed partial class SectorReferenceTracker
{
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
}
