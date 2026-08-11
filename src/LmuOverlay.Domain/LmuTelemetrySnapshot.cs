namespace LmuOverlay.Domain;

public sealed record LmuTelemetrySnapshot(
    LmuConnectionState State,
    int GameVersion,
    uint ScoringSequence,
    uint TelemetrySequence,
    int ActiveVehicles,
    int ScoredVehicles,
    LmuSessionSnapshot? Session,
    LmuPlayerTelemetry? Player,
    IReadOnlyList<LmuVehicleStanding> Standings,
    DateTimeOffset CapturedAt,
    string Detail)
{
    public static LmuTelemetrySnapshot Unavailable(
        LmuConnectionState state,
        string detail) =>
        new(
            state,
            0,
            0,
            0,
            0,
            0,
            null,
            null,
            Array.Empty<LmuVehicleStanding>(),
            DateTimeOffset.UtcNow,
            detail);
}

public sealed record LmuSessionSnapshot(
    string TrackName,
    int SessionCode,
    LmuSessionKind Kind,
    LmuGamePhase GamePhase,
    double CurrentElapsedTime,
    double EndElapsedTime,
    int MaximumLaps,
    double LapLengthMeters,
    bool IsRealtime,
    string PlayerName,
    LmuWeatherSnapshot Weather);

public sealed record LmuWeatherSnapshot(
    double Cloudiness,
    double RainIntensity,
    double AmbientTemperatureCelsius,
    double TrackTemperatureCelsius,
    LmuVector3 WindMetersPerSecond,
    double MinimumPathWetness,
    double MaximumPathWetness,
    double AveragePathWetness,
    int TrackGripLevel);

public sealed record LmuPlayerTelemetry(
    int VehicleId,
    string VehicleName,
    string VehicleModel,
    int VehicleClassId,
    int LapNumber,
    double LapStartElapsedTime,
    double LapDistanceMeters,
    int Position,
    int Gear,
    int MaximumGears,
    double EngineRpm,
    double EngineMaximumRpm,
    double EngineWaterTemperatureCelsius,
    double EngineOilTemperatureCelsius,
    double RearBrakeBiasFraction,
    double SpeedKilometersPerHour,
    double Throttle,
    double Brake,
    double Steering,
    double Clutch,
    double FuelLiters,
    double FuelCapacityLiters,
    double DeltaBestSeconds,
    double BatteryChargeFraction,
    double StateOfCharge,
    double RegenerationKilowatts,
    double VirtualEnergy,
    double GapToCarAheadSeconds,
    double GapToCarBehindSeconds,
    int CurrentSector,
    bool SpeedLimiterActive,
    bool LapInvalidated,
    bool AbsActive,
    bool TractionControlActive,
    int TractionControlLevel,
    int TractionControlMaximum,
    int TractionControlSlipLevel,
    int TractionControlSlipMaximum,
    int TractionControlCutLevel,
    int TractionControlCutMaximum,
    int AbsLevel,
    int AbsMaximum,
    LmuWheelTemperatures TireTemperatures,
    LmuWheelWear TireWear,
    LmuDamageSnapshot? Damage = null,
    string FrontTireCompound = "",
    string RearTireCompound = "",
    int FrontTireCompoundIndex = 0,
    int RearTireCompoundIndex = 0,
    LmuVector3 LocalAcceleration = default)
{
    public double ElapsedTime { get; init; }
}

public readonly record struct LmuWheelTemperatures(
    double FrontLeftCelsius,
    double FrontRightCelsius,
    double RearLeftCelsius,
    double RearRightCelsius);

public readonly record struct LmuWheelWear(
    double FrontLeftFraction,
    double FrontRightFraction,
    double RearLeftFraction,
    double RearRightFraction);

public sealed record LmuDamageSnapshot(
    int ScheduledStops,
    bool Overheating,
    bool BodyPartDetached,
    int MaximumDentSeverity,
    int DamagedAreas,
    double LastImpactElapsedTime,
    double LastImpactMagnitude,
    LmuWheelCondition FrontLeft,
    LmuWheelCondition FrontRight,
    LmuWheelCondition RearLeft,
    LmuWheelCondition RearRight)
{
    public bool HasCriticalDamage =>
        Overheating ||
        BodyPartDetached ||
        FrontLeft.Flat || FrontLeft.Detached ||
        FrontRight.Flat || FrontRight.Detached ||
        RearLeft.Flat || RearLeft.Detached ||
        RearRight.Flat || RearRight.Detached;
}

public sealed record LmuWheelCondition(bool Flat, bool Detached);

public sealed record LmuVehicleStanding(
    int VehicleId,
    string DriverName,
    string VehicleName,
    string VehicleModel,
    string VehicleClass,
    int Position,
    int CompletedLaps,
    int Sector,
    double LapDistanceMeters,
    double BestLapTimeSeconds,
    double LastLapTimeSeconds,
    double GapToNextSeconds,
    int LapsBehindNext,
    double GapToLeaderSeconds,
    int LapsBehindLeader,
    int PitStops,
    int Penalties,
    bool IsPlayer,
    bool IsInPits,
    LmuPitState PitState,
    int Flag,
    bool IsUnderYellow,
    bool IsInGarage,
    double FuelFraction,
    bool DrsActive,
    double BestSector1Seconds = 0,
    double BestSector2CumulativeSeconds = 0,
    double LastSector1Seconds = 0,
    double LastSector2CumulativeSeconds = 0,
    double CurrentSector1Seconds = 0,
    double CurrentSector2CumulativeSeconds = 0,
    string FrontTireCompound = "",
    string RearTireCompound = "",
    int FrontTireCompoundIndex = 0,
    int RearTireCompoundIndex = 0,
    double VirtualEnergyFraction = -1,
    double BestLapSector1Seconds = 0,
    double BestLapSector2CumulativeSeconds = 0);

public readonly record struct LmuVector3(double X, double Y, double Z)
{
    public double Length => Math.Sqrt((X * X) + (Y * Y) + (Z * Z));
}

public enum LmuSessionKind
{
    Unknown,
    TestDay,
    Practice,
    Qualifying,
    Warmup,
    Race
}

public enum LmuGamePhase : byte
{
    BeforeSession = 0,
    Reconnaissance = 1,
    GridWalk = 2,
    FormationLap = 3,
    StartingLights = 4,
    GreenFlag = 5,
    FullCourseYellow = 6,
    Stopped = 7,
    SessionOver = 8,
    Paused = 9
}

public enum LmuPitState : byte
{
    None = 0,
    Requested = 1,
    Entering = 2,
    Stopped = 3,
    Exiting = 4
}
