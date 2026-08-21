using LmuOverlay.Domain;

namespace LmuOverlay.Widgets;

public sealed class PersistentSectorReferenceTracker
{
    private SectorReferenceTracker _tracker = new();
    private readonly SectorReferenceStore _sectorStore;
    private readonly PersonalBestLapStore _personalBestStore;
    private string _trackName = string.Empty;
    private string _driverName = string.Empty;
    private string _vehicleModel = string.Empty;
    private SectorReferenceSeed _saved;
    private PersonalBestLap _personalBest;
    private double _personalOptimal;
    private int _persistenceRevision;

    public PersistentSectorReferenceTracker(
        SectorReferenceStore sectorStore,
        PersonalBestLapStore personalBestStore)
    {
        _sectorStore = sectorStore;
        _personalBestStore = personalBestStore;
    }

    public double PersonalBestLapTimeSeconds =>
        _personalBest.IsValid ? _personalBest.LapTimeSeconds : 0;
    public double PersonalOptimalLapTimeSeconds => _personalOptimal;

    public DashboardSectorTimes Update(
        LmuTelemetrySnapshot snapshot,
        DashboardSectorTimes observed)
    {
        if (snapshot.Session is not { } session || snapshot.Player is not { } player)
        {
            return _tracker.Update(snapshot, observed);
        }

        var vehicleModel = string.IsNullOrWhiteSpace(player.VehicleModel)
            ? player.VehicleName
            : player.VehicleModel;
        var playerStanding = snapshot.Standings.FirstOrDefault(
            item => item.IsPlayer || item.VehicleId == player.VehicleId);
        var driverName = playerStanding?.DriverName;
        if (string.IsNullOrWhiteSpace(driverName))
        {
            driverName = session.PlayerName;
        }
        if (!string.Equals(_trackName, session.TrackName, StringComparison.Ordinal) ||
            !string.Equals(_driverName, driverName, StringComparison.Ordinal) ||
            !string.Equals(_vehicleModel, vehicleModel, StringComparison.Ordinal))
        {
            _trackName = session.TrackName;
            _driverName = driverName ?? string.Empty;
            _vehicleModel = vehicleModel;
            var timing = _personalBestStore.LoadRecord(
                _trackName,
                _driverName,
                _vehicleModel);
            _personalBest = timing.BestLap;
            _personalOptimal = timing.OptimalLapTimeSeconds;
            _saved = timing.BestSectors;
            if (_saved == default)
            {
                _saved = _sectorStore.Load(_trackName, _vehicleModel);
            }
            _tracker = new SectorReferenceTracker();
            _persistenceRevision = 0;
        }

        var result = _tracker.Update(snapshot, observed, _saved);
        if (_tracker.PersistenceRevision != _persistenceRevision)
        {
            _persistenceRevision = _tracker.PersistenceRevision;
            var validSectorRecords = _tracker.PersistentReferences;
            _sectorStore.Save(_trackName, _vehicleModel, validSectorRecords);
            _saved = _personalBestStore.SaveSectorsIfFaster(
                _trackName,
                _driverName,
                _vehicleModel,
                validSectorRecords);
            _personalOptimal = _saved.Optimal;
        }
        var officialBest = OfficialPersonalBest(playerStanding);
        var isNewPersonalBest = officialBest.IsValid &&
            (!_personalBest.IsValid ||
             officialBest.LapTimeSeconds < _personalBest.LapTimeSeconds - 0.0005);
        if (isNewPersonalBest)
        {
            _personalBest = _personalBestStore.SaveIfFaster(
                _trackName,
                _driverName,
                _vehicleModel,
                officialBest);
            var timing = _personalBestStore.LoadRecord(
                _trackName, _driverName, _vehicleModel);
            _saved = timing.BestSectors;
            _personalOptimal = timing.OptimalLapTimeSeconds;
        }

        var hasSavedSectors = _saved.Sector1Seconds > 0 ||
            _saved.Sector2Seconds > 0 ||
            _saved.Sector3Seconds > 0;
        return hasSavedSectors
            ? result with
            {
                BestSector1Seconds = _saved.Sector1Seconds,
                BestSector2Seconds = _saved.Sector2Seconds,
                BestSector3Seconds = _saved.Sector3Seconds,
                Sector1ReferenceOrigin = SectorReferenceOrigin.Saved,
                Sector2ReferenceOrigin = SectorReferenceOrigin.Saved,
                Sector3ReferenceOrigin = SectorReferenceOrigin.Saved,
                RecentSectorReferenceSeconds =
                    result.RecentSectorIndex >= 0
                        ? Sector(_saved, result.RecentSectorIndex)
                        : result.RecentSectorReferenceSeconds,
            }
            : result;
    }

    public double ObserveOptimal(LmuTelemetrySnapshot snapshot, double optimalLapTimeSeconds)
    {
        if (snapshot.Session is not { } session || snapshot.Player is not { } player ||
            !double.IsFinite(optimalLapTimeSeconds) || optimalLapTimeSeconds is <= 10 or >= 1_800)
        {
            return PersonalOptimalLapTimeSeconds;
        }

        var standing = snapshot.Standings.FirstOrDefault(item =>
            item.IsPlayer || item.VehicleId == player.VehicleId);
        var driver = !string.IsNullOrWhiteSpace(standing?.DriverName)
            ? standing.DriverName
            : session.PlayerName;
        var model = string.IsNullOrWhiteSpace(player.VehicleModel)
            ? player.VehicleName
            : player.VehicleModel;
        if (!string.Equals(_trackName, session.TrackName, StringComparison.Ordinal) ||
            !string.Equals(_driverName, driver, StringComparison.Ordinal) ||
            !string.Equals(_vehicleModel, model, StringComparison.Ordinal))
        {
            return 0;
        }

        _personalOptimal = _personalBestStore.SaveOptimalIfFaster(
            _trackName, _driverName, _vehicleModel, optimalLapTimeSeconds);
        return _personalOptimal;
    }

    private static double Sector(SectorReferenceSeed sectors, int index) => index switch
    {
        0 => sectors.Sector1Seconds,
        1 => sectors.Sector2Seconds,
        2 => sectors.Sector3Seconds,
        _ => 0,
    };

    public static PersonalBestLap OfficialPersonalBest(
        LmuVehicleStanding? standing)
    {
        if (standing is null)
        {
            return default;
        }

        // LMU publishes the second best-lap split as elapsed time at the end
        // of S2 (S1 + S2), matching the other scoring sector-2 fields.
        // Convert both remaining sectors to individual durations before
        // validating or persisting the official lap.
        var sector2 = standing.BestLapSector2CumulativeSeconds -
            standing.BestLapSector1Seconds;
        var sector3 = standing.BestLapTimeSeconds -
            standing.BestLapSector2CumulativeSeconds;
        return new(
            standing.BestLapTimeSeconds,
            standing.BestLapSector1Seconds,
            sector2,
            sector3);
    }
}
