namespace LmuOverlay.LmuSharedMemory;

public static class LmuApiLayoutV1
{
    public const string MapName = "LMU_Data";
    public const string EventName = "LMU_Data_Event";
    public const int MaximumVehicles = 104;
    public const int ObjectSize = 324_820;

    public const int EventCount = 16;
    public const int EventSize = sizeof(uint);
    public const int ScoringUpdateEventIndex = 10;
    public const int TelemetryUpdateEventIndex = 11;

    public const int GameVersionOffset = 64;
    public const int GameVersionLength = 4;

    public const int ScoringOffset = 1_632;
    public const int TrackNameOffset = ScoringOffset;
    public const int TrackNameLength = 64;
    public const int SessionCodeOffset = ScoringOffset + 64;
    public const int ScoredVehiclesOffset = ScoringOffset + 104;
    public const int VehicleScoringArrayOffset = ScoringOffset + 560;
    public const int VehicleScoringSize = 584;
    public const int VehicleScoringIsPlayerOffset = 196;

    public const int TelemetryOffset = 128_464;
    public const int ActiveVehiclesOffset = TelemetryOffset;
    public const int PlayerVehicleIndexOffset = TelemetryOffset + 1;
    public const int PlayerHasVehicleOffset = TelemetryOffset + 2;
    public const int VehicleTelemetryArrayOffset = TelemetryOffset + 4;
    public const int VehicleTelemetrySize = 1_888;
    public const int VehicleNameOffset = 32;
    public const int VehicleNameLength = 64;

    public static int EventOffset(int eventIndex) => eventIndex * EventSize;

    public static int VehicleTelemetryOffset(int vehicleIndex) =>
        VehicleTelemetryArrayOffset + (vehicleIndex * VehicleTelemetrySize);
}
