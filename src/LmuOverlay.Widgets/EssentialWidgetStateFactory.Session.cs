using LmuOverlay.Domain;

namespace LmuOverlay.Widgets;

public static partial class EssentialWidgetStateFactory
{
    public static SessionFlagsWidgetState CreateSessionFlags(
        LmuTelemetrySnapshot snapshot)
    {
        if (snapshot.Session is not { } session)
        {
            return new(
                false,
                string.Empty,
                string.Empty,
                "NO DATA",
                "UNKNOWN",
                -1,
                WeatherConditionKind.Unknown,
                "NO DATA",
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0);
        }

        var playerStanding = snapshot.Standings.FirstOrDefault(item => item.IsPlayer);
        var globalYellow = session.GamePhase == LmuGamePhase.FullCourseYellow ||
            snapshot.Standings.Any(item => item.IsUnderYellow);
        var flagName = globalYellow
            ? "YELLOW"
            : session.GamePhase == LmuGamePhase.GreenFlag
                ? "GREEN"
                : session.GamePhase == LmuGamePhase.Stopped
                    ? "RED"
                : session.GamePhase.ToString().ToUpperInvariant();
        var weatherCondition = ClassifyWeather(
            session.Weather.Cloudiness,
            session.Weather.RainIntensity);

        return new(
            true,
            FormatSessionKind(session.Kind),
            FormatGamePhase(session.GamePhase),
            flagName,
            FormatTrackGrip(session.Weather.TrackGripLevel),
            session.Weather.TrackGripLevel,
            weatherCondition,
            FormatWeather(weatherCondition),
            session.Weather.RainIntensity,
            session.Weather.Cloudiness,
            session.Weather.AveragePathWetness,
            session.EndElapsedTime > 0
                ? Math.Max(0, session.EndElapsedTime - session.CurrentElapsedTime)
                : 0,
            playerStanding?.CompletedLaps ?? snapshot.Player?.LapNumber ?? 0,
            LmuSessionLimits.NormalizeMaximumLaps(session.MaximumLaps),
            session.Weather.AmbientTemperatureCelsius,
            session.Weather.TrackTemperatureCelsius);
    }

    private static string FormatTrackGrip(int level) => level switch
    {
        0 => "GREEN",
        1 => "LIGHT",
        2 => "MEDIUM",
        3 => "HEAVY",
        >= 4 => "SATURATED",
        _ => "UNKNOWN",
    };

    private static WeatherConditionKind ClassifyWeather(
        double cloudiness,
        double rainIntensity) =>
        rainIntensity switch
        {
            >= 0.65 => WeatherConditionKind.HeavyRain,
            >= 0.25 => WeatherConditionKind.Rain,
            >= 0.02 => WeatherConditionKind.LightRain,
            _ => cloudiness switch
            {
                >= 0.75 => WeatherConditionKind.Overcast,
                >= 0.35 => WeatherConditionKind.Cloudy,
                >= 0.10 => WeatherConditionKind.PartlyCloudy,
                _ => WeatherConditionKind.Clear,
            },
        };

    private static string FormatWeather(WeatherConditionKind condition) =>
        condition switch
        {
            WeatherConditionKind.Clear => "CLEAR",
            WeatherConditionKind.PartlyCloudy => "PARTLY CLOUDY",
            WeatherConditionKind.Cloudy => "CLOUDY",
            WeatherConditionKind.Overcast => "OVERCAST",
            WeatherConditionKind.LightRain => "LIGHT RAIN",
            WeatherConditionKind.Rain => "RAIN",
            WeatherConditionKind.HeavyRain => "HEAVY RAIN",
            _ => "UNKNOWN",
        };

    private static string FormatGear(int gear) => gear switch
    {
        < 0 => "R",
        0 => "N",
        _ => gear.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    private static string FormatSessionKind(LmuSessionKind kind) => kind switch
    {
        LmuSessionKind.TestDay => "TEST DAY",
        LmuSessionKind.Qualifying => "QUALIFYING",
        _ => kind.ToString().ToUpperInvariant(),
    };

    private static double SessionRemaining(LmuSessionSnapshot? session) =>
        session is { EndElapsedTime: > 0 } &&
        double.IsFinite(session.EndElapsedTime) &&
        double.IsFinite(session.CurrentElapsedTime)
            ? Math.Max(0, session.EndElapsedTime - session.CurrentElapsedTime)
            : 0;

    private static string FormatGamePhase(LmuGamePhase phase) => phase switch
    {
        LmuGamePhase.BeforeSession => "BEFORE SESSION",
        LmuGamePhase.GridWalk => "GRID WALK",
        LmuGamePhase.FormationLap => "FORMATION LAP",
        LmuGamePhase.StartingLights => "STARTING LIGHTS",
        LmuGamePhase.GreenFlag => "GREEN FLAG",
        LmuGamePhase.FullCourseYellow => "FULL COURSE YELLOW",
        LmuGamePhase.SessionOver => "SESSION OVER",
        _ => phase.ToString().ToUpperInvariant(),
    };

    private static double ClampInput(double value) => Math.Clamp(value, 0, 1);

    private static double NormalizeVirtualEnergy(double value) =>
        double.IsFinite(value) && value is >= 0 and <= 1
            ? value
            : -1;
}
