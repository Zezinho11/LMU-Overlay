namespace LmuOverlay.Domain;

public sealed record LmuProbeSnapshot(
    LmuConnectionState State,
    string GameVersion,
    string TrackName,
    int SessionCode,
    string PlayerVehicleName,
    int ActiveVehicles,
    int ScoredVehicles,
    bool HasPlayerVehicle,
    DateTimeOffset CapturedAt,
    string Detail)
{
    public static LmuProbeSnapshot Disconnected(string detail) =>
        new(
            LmuConnectionState.Disconnected,
            string.Empty,
            string.Empty,
            0,
            string.Empty,
            0,
            0,
            false,
            DateTimeOffset.UtcNow,
            detail);
}
