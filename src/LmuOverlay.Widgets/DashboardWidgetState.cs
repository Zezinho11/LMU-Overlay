using LmuOverlay.Domain;

namespace LmuOverlay.Widgets;

public readonly record struct DashboardWidgetState(
    bool Available, double SpeedKilometersPerHour, string Gear, double EngineRpm,
    double EngineRpmFraction, double FuelLiters, int Position, int LapNumber,
    string TrackName, double DeltaBestSeconds, double CurrentLapTimeSeconds,
    double LastLapTimeSeconds, double BestLapTimeSeconds,
    double EngineWaterTemperatureCelsius, double EngineOilTemperatureCelsius,
    double RearBrakeBiasFraction, bool SpeedLimiterActive, bool AbsActive,
    bool TractionControlActive, int TractionControlLevel, int TractionControlMaximum,
    int TractionControlSlipLevel, int TractionControlSlipMaximum,
    int TractionControlCutLevel, int TractionControlCutMaximum,
    int AbsLevel, int AbsMaximum, LmuWheelTemperatures TireTemperatures,
    LmuWheelWear TireWear, double Throttle = 0, double Brake = 0,
    double LongitudinalAccelerationG = 0, double LateralAccelerationG = 0,
    double AmbientTemperatureCelsius = 0, double TrackTemperatureCelsius = 0,
    double RainIntensity = 0, double SessionRemainingSeconds = 0,
    string SessionName = "", int OutstandingPenalties = 0,
    string TireCompound = "", DashboardSectorTimes SectorTimes = default,
    double VirtualEnergyFraction = 0)
{
    public double OptimalLapTimeSeconds { get; init; }
    public string VehicleClass { get; init; } = "";
    public string VehicleModel { get; init; } = "";
}

public readonly record struct InputsWidgetState(
    bool Available, double Throttle, double Brake, double Clutch, double Steering,
    bool AbsActive, bool TractionControlActive, double SteeringWheelRangeDegrees = 0);
