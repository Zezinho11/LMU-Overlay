using System.Diagnostics;
using LmuOverlay.Domain;

namespace LmuOverlay.Widgets;

public sealed partial class SectorReferenceTracker
{
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
}
