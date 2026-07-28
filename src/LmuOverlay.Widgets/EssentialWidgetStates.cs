using LmuOverlay.Domain;

namespace LmuOverlay.Widgets;

public sealed record DashboardWidgetState(
    bool Available,
    double SpeedKilometersPerHour,
    string Gear,
    double EngineRpm,
    double EngineRpmFraction,
    double FuelLiters,
    int Position,
    int LapNumber,
    string TrackName,
    double DeltaBestSeconds,
    double CurrentLapTimeSeconds,
    double LastLapTimeSeconds,
    double BestLapTimeSeconds,
    double EngineWaterTemperatureCelsius,
    double EngineOilTemperatureCelsius,
    double RearBrakeBiasFraction,
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
    LmuWheelTemperatures TireTemperatures);

public sealed record InputsWidgetState(
    bool Available,
    double Throttle,
    double Brake,
    double Clutch,
    double Steering,
    bool AbsActive,
    bool TractionControlActive);

public sealed record LiveStandingsWidgetState(
    string PlayerClass,
    IReadOnlyList<LiveStandingsClassState> Classes);

public sealed record LiveStandingsClassState(
    string ClassName,
    bool IsPlayerClass,
    IReadOnlyList<LiveStandingsRowState> Rows);

public sealed record LiveStandingsRowState(
    int ClassPosition,
    string DriverName,
    string DriverAbbreviation,
    string VehicleName,
    string VehicleModel,
    string CarNumber,
    int CompletedLaps,
    double GapToLeaderSeconds,
    double IntervalSeconds,
    int IntervalLaps,
    double LastLapTimeSeconds,
    double BestLapTimeSeconds,
    bool IsPlayer,
    bool IsInPitLane);

public sealed record RelativeWidgetState(
    IReadOnlyList<RelativeRowState> Rows);

public sealed record RelativeRowState(
    int OverallPosition,
    string DriverName,
    string DriverDisplayName,
    string VehicleClass,
    string ClassAbbreviation,
    string CarNumber,
    double RelativeGapSeconds,
    int RelativeLaps,
    bool IsPlayer,
    bool IsInPitLane);

public sealed record SessionFlagsWidgetState(
    bool Available,
    string SessionName,
    string PhaseName,
    string FlagName,
    string TrackGripName,
    int TrackGripLevel,
    WeatherConditionKind WeatherCondition,
    string WeatherName,
    double RainIntensity,
    double Cloudiness,
    double AveragePathWetness,
    double RemainingSeconds,
    int CurrentLap,
    int MaximumLaps,
    double AmbientTemperatureCelsius,
    double TrackTemperatureCelsius);

public enum WeatherConditionKind
{
    Unknown,
    Clear,
    PartlyCloudy,
    Cloudy,
    Overcast,
    LightRain,
    Rain,
    HeavyRain,
}

public static class EssentialWidgetStateFactory
{
    private const int LiveStandingsContentHeight = 388;
    private const int LiveStandingsClassHeaderHeight = 18;
    private const int LiveStandingsRowHeight = 25;
    private const int MaximumOtherClasses = 2;

    public static DashboardWidgetState CreateDashboard(LmuTelemetrySnapshot snapshot)
    {
        if (snapshot.Player is not { } player)
        {
            return new(
                false, 0, "N", 0, 0, 0, 0, 0,
                snapshot.Session?.TrackName ?? string.Empty,
                0, 0, 0, 0, 0, 0, 0, false, false,
                0, 0, 0, 0, 0, 0, 0, 0,
                new LmuWheelTemperatures(0, 0, 0, 0));
        }

        var session = snapshot.Session;
        var playerStanding = snapshot.Standings.FirstOrDefault(item => item.IsPlayer);
        return new(
            true,
            player.SpeedKilometersPerHour,
            FormatGear(player.Gear),
            player.EngineRpm,
            player.EngineMaximumRpm > 0
                ? Math.Clamp(player.EngineRpm / player.EngineMaximumRpm, 0, 1)
                : 0,
            player.FuelLiters,
            player.Position,
            player.LapNumber,
            session?.TrackName ?? string.Empty,
            player.DeltaBestSeconds,
            session is not null &&
            session.CurrentElapsedTime >= player.LapStartElapsedTime
                ? session.CurrentElapsedTime - player.LapStartElapsedTime
                : 0,
            playerStanding?.LastLapTimeSeconds ?? 0,
            playerStanding?.BestLapTimeSeconds ?? 0,
            player.EngineWaterTemperatureCelsius,
            player.EngineOilTemperatureCelsius,
            player.RearBrakeBiasFraction,
            player.AbsActive,
            player.TractionControlActive,
            player.TractionControlLevel,
            player.TractionControlMaximum,
            player.TractionControlSlipLevel,
            player.TractionControlSlipMaximum,
            player.TractionControlCutLevel,
            player.TractionControlCutMaximum,
            player.AbsLevel,
            player.AbsMaximum,
            player.TireTemperatures);
    }

    public static InputsWidgetState CreateInputs(LmuTelemetrySnapshot snapshot)
    {
        if (snapshot.Player is not { } player)
        {
            return new(false, 0, 0, 0, 0, false, false);
        }

        return new(
            true,
            ClampInput(player.Throttle),
            ClampInput(player.Brake),
            ClampInput(player.Clutch),
            Math.Clamp(player.Steering, -1, 1),
            player.AbsActive,
            player.TractionControlActive);
    }

    public static LiveStandingsWidgetState CreateLiveStandings(
        LmuTelemetrySnapshot snapshot)
    {
        var ordered = snapshot.Standings
            .OrderBy(item => item.Position)
            .ToArray();
        var playerClass = ordered.FirstOrDefault(item => item.IsPlayer)?.VehicleClass
            ?? string.Empty;

        var groupedClasses = ordered
            .GroupBy(item => string.IsNullOrWhiteSpace(item.VehicleClass)
                ? "Unknown"
                : item.VehicleClass)
            .ToArray();
        var selectedGroups = groupedClasses
            .Where(group => string.Equals(
                group.Key,
                playerClass,
                StringComparison.OrdinalIgnoreCase))
            .Concat(groupedClasses
                .Where(group => !string.Equals(
                    group.Key,
                    playerClass,
                    StringComparison.OrdinalIgnoreCase))
                .Take(MaximumOtherClasses))
            .ToArray();
        var otherClassCount = selectedGroups.Count(group => !string.Equals(
            group.Key,
            playerClass,
            StringComparison.OrdinalIgnoreCase));
        var visibleCarCapacity = Math.Max(
            2,
            (LiveStandingsContentHeight -
             (selectedGroups.Length * LiveStandingsClassHeaderHeight)) /
            LiveStandingsRowHeight);
        var playerClassLimit = Math.Max(2, visibleCarCapacity - otherClassCount);

        var classes = selectedGroups
            .Select(group =>
            {
                var isPlayerClass = string.Equals(
                    group.Key,
                    playerClass,
                    StringComparison.OrdinalIgnoreCase);
                var classOrder = group.OrderBy(item => item.Position).ToArray();
                var visible = isPlayerClass
                    ? SelectPlayerClassWindow(classOrder, playerClassLimit)
                    : classOrder.Take(1);
                var rows = visible.Select(item => new LiveStandingsRowState(
                    Array.IndexOf(classOrder, item) + 1,
                    item.DriverName,
                    AbbreviateDriverName(item.DriverName),
                    item.VehicleName,
                    item.VehicleModel,
                    ExtractCarNumber(item.VehicleName),
                    item.CompletedLaps,
                    item.GapToLeaderSeconds,
                    item.GapToNextSeconds,
                    item.LapsBehindNext,
                    item.LastLapTimeSeconds,
                    item.BestLapTimeSeconds,
                    item.IsPlayer,
                    item.IsInPits || item.PitState is not LmuPitState.None))
                    .ToArray();
                return new LiveStandingsClassState(
                    group.Key,
                    isPlayerClass,
                    rows);
            })
            .OrderByDescending(item => item.IsPlayerClass)
            .ThenBy(item => item.ClassName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new LiveStandingsWidgetState(playerClass, classes);
    }

    private static IEnumerable<LmuVehicleStanding> SelectPlayerClassWindow(
        LmuVehicleStanding[] ordered,
        int maximumRows)
    {
        if (ordered.Length <= maximumRows)
        {
            return ordered;
        }

        var playerIndex = Array.FindIndex(ordered, item => item.IsPlayer);
        if (playerIndex <= 0)
        {
            return ordered.Take(maximumRows);
        }

        var surroundingRows = maximumRows - 1;
        var maximumStart = Math.Max(1, ordered.Length - surroundingRows);
        var start = Math.Clamp(playerIndex - (surroundingRows / 2), 1, maximumStart);
        return ordered.Take(1).Concat(ordered.Skip(start).Take(surroundingRows));
    }

    private static string AbbreviateDriverName(string driverName)
    {
        var normalized = driverName.Trim();
        if (normalized.Length == 0)
        {
            return "---";
        }

        var lastName = normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Last();
        var letters = new string(lastName
            .Normalize(System.Text.NormalizationForm.FormD)
            .Where(character =>
                System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character) !=
                System.Globalization.UnicodeCategory.NonSpacingMark &&
                char.IsLetterOrDigit(character))
            .Take(3)
            .ToArray());
        return letters.PadRight(3, '-').ToUpperInvariant();
    }

    private static string ExtractCarNumber(string vehicleName)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            vehicleName,
            @"(?:^|\s)#(?<number>\d{1,3})(?:\s|$)",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["number"].Value : "--";
    }

    public static RelativeWidgetState CreateRelative(
        LmuTelemetrySnapshot snapshot,
        int carsEachSide = 4)
    {
        var ordered = snapshot.Standings.OrderBy(item => item.Position).ToArray();
        var playerIndex = Array.FindIndex(ordered, item => item.IsPlayer);
        if (playerIndex < 0)
        {
            return new RelativeWidgetState(Array.Empty<RelativeRowState>());
        }

        var player = ordered[playerIndex];
        var start = Math.Max(0, playerIndex - Math.Max(1, carsEachSide));
        var end = Math.Min(ordered.Length - 1, playerIndex + Math.Max(1, carsEachSide));
        var rows = ordered[start..(end + 1)]
            .Select(item => new RelativeRowState(
                item.Position,
                item.DriverName,
                AbbreviateRelativeDriverName(item.DriverName),
                item.VehicleClass,
                AbbreviateVehicleClass(item.VehicleClass),
                ExtractCarNumber(item.VehicleName),
                item.IsPlayer ? 0 : item.GapToLeaderSeconds - player.GapToLeaderSeconds,
                player.CompletedLaps - item.CompletedLaps,
                item.IsPlayer,
                item.IsInPits || item.PitState is not LmuPitState.None))
            .ToArray();
        return new RelativeWidgetState(rows);
    }

    private static string AbbreviateRelativeDriverName(string driverName)
    {
        var parts = driverName
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "---",
            1 => parts[0],
            _ => $"{char.ToUpperInvariant(parts[0][0])} {parts[^1]}",
        };
    }

    private static string AbbreviateVehicleClass(string vehicleClass)
    {
        var value = vehicleClass.Trim().ToUpperInvariant();
        return value switch
        {
            _ when value.Contains("GT3", StringComparison.Ordinal) ||
                       value.Contains("LMGT", StringComparison.Ordinal) => "GT3",
            _ when value.Contains("HYPER", StringComparison.Ordinal) => "HYP",
            _ when value.Contains("LMP2", StringComparison.Ordinal) => "P2",
            _ => new string(value.Where(char.IsLetterOrDigit).Take(3).ToArray())
                .PadRight(3, '-'),
        };
    }

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
}
