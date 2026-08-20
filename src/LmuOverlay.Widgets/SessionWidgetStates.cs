namespace LmuOverlay.Widgets;

public sealed record SessionFlagsWidgetState(
    bool Available, string SessionName, string PhaseName, string FlagName,
    string TrackGripName, int TrackGripLevel, WeatherConditionKind WeatherCondition,
    string WeatherName, double RainIntensity, double Cloudiness,
    double AveragePathWetness, double RemainingSeconds, int CurrentLap,
    int MaximumLaps, double AmbientTemperatureCelsius, double TrackTemperatureCelsius);

public sealed record RaceControlWidgetState(
    bool Available, int OutstandingPenalties, string PenaltyStatus, string PitStatus,
    string LapStatus, string FlagStatus, string DamageStatus, string ImpactStatus,
    string SystemsStatus, bool RequiresAttention, bool HasCriticalDamage);

public enum WeatherConditionKind
{
    Unknown, Clear, PartlyCloudy, Cloudy, Overcast, LightRain, Rain, HeavyRain,
}
