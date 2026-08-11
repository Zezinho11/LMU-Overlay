using LmuOverlay.Domain;
using LmuOverlay.Widgets;

namespace LmuOverlay.Desktop;

internal sealed class PersistentSectorReferenceTracker
{
    private SectorReferenceTracker _tracker = new();
    private readonly SectorReferenceStore _sectorStore;
    private readonly PersonalBestLapStore _personalBestStore;
    private string _trackName = string.Empty;
    private string _driverName = string.Empty;
    private string _vehicleModel = string.Empty;
    private SectorReferenceSeed _saved;
    private PersonalBestLap _personalBest;

    public PersistentSectorReferenceTracker(
        SectorReferenceStore sectorStore,
        PersonalBestLapStore personalBestStore)
    {
        _sectorStore = sectorStore;
        _personalBestStore = personalBestStore;
    }

    public double PersonalBestLapTimeSeconds =>
        _personalBest.IsValid ? _personalBest.LapTimeSeconds : 0;

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
            _personalBest = _personalBestStore.Load(
                _trackName,
                _driverName,
                _vehicleModel);
            _saved = _sectorStore.Load(_trackName, _vehicleModel);
            _tracker = new SectorReferenceTracker();
        }

        var result = _tracker.Update(snapshot, observed, _saved);
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
            _sectorStore.Save(
                _trackName,
                _vehicleModel,
                officialBest.Sectors);
            _saved = _sectorStore.Load(_trackName, _vehicleModel);
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

    private static double Sector(SectorReferenceSeed sectors, int index) => index switch
    {
        0 => sectors.Sector1Seconds,
        1 => sectors.Sector2Seconds,
        2 => sectors.Sector3Seconds,
        _ => 0,
    };

    internal static PersonalBestLap OfficialPersonalBest(
        LmuVehicleStanding? standing)
    {
        if (standing is null)
        {
            return default;
        }

        var sector3 = standing.BestLapTimeSeconds -
            standing.BestLapSector1Seconds -
            standing.BestLapSector2Seconds;
        return new(
            standing.BestLapTimeSeconds,
            standing.BestLapSector1Seconds,
            standing.BestLapSector2Seconds,
            sector3);
    }
}
