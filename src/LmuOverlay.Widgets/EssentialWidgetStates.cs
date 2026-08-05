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
    bool SpeedLimiterActive,
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
    double Throttle = 0,
    double Brake = 0,
    double LongitudinalAccelerationG = 0,
    double LateralAccelerationG = 0,
    double AmbientTemperatureCelsius = 0,
    double TrackTemperatureCelsius = 0,
    double RainIntensity = 0,
    double SessionRemainingSeconds = 0,
    string SessionName = "",
    int OutstandingPenalties = 0,
    string TireCompound = "",
    DashboardSectorTimes SectorTimes = default,
    double VirtualEnergyFraction = 0);

public readonly record struct DashboardSectorTimes(
    double CurrentSector1Seconds,
    double CurrentSector2Seconds,
    double CurrentSector3Seconds,
    double LastSector1Seconds,
    double LastSector2Seconds,
    double LastSector3Seconds,
    double BestSector1Seconds,
    double BestSector2Seconds,
    double BestSector3Seconds);

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

public sealed record RaceControlWidgetState(
    bool Available,
    int OutstandingPenalties,
    string PenaltyStatus,
    string PitStatus,
    string LapStatus,
    string FlagStatus,
    string DamageStatus,
    string ImpactStatus,
    string SystemsStatus,
    bool RequiresAttention,
    bool HasCriticalDamage);

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
                0, 0, 0, 0, 0, 0, 0, false, false, false,
                0, 0, 0, 0, 0, 0, 0, 0,
                new LmuWheelTemperatures(0, 0, 0, 0),
                new LmuWheelWear(0, 0, 0, 0));
        }

        var session = snapshot.Session;
        var playerStanding = snapshot.Standings.FirstOrDefault(item => item.IsPlayer);
        var currentElapsedTime = player.ElapsedTime > 0
            ? player.ElapsedTime
            : session?.CurrentElapsedTime ?? 0;
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
            currentElapsedTime >= player.LapStartElapsedTime
                ? currentElapsedTime - player.LapStartElapsedTime
                : 0,
            playerStanding?.LastLapTimeSeconds ?? 0,
            playerStanding?.BestLapTimeSeconds ?? 0,
            player.EngineWaterTemperatureCelsius,
            player.EngineOilTemperatureCelsius,
            player.RearBrakeBiasFraction,
            player.SpeedLimiterActive,
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
            player.TireTemperatures,
            player.TireWear)
        {
            Throttle = ClampInput(player.Throttle),
            Brake = ClampInput(player.Brake),
            LongitudinalAccelerationG = -player.LocalAcceleration.Z / 9.80665,
            LateralAccelerationG = player.LocalAcceleration.X / 9.80665,
            AmbientTemperatureCelsius = session?.Weather.AmbientTemperatureCelsius ?? 0,
            TrackTemperatureCelsius = session?.Weather.TrackTemperatureCelsius ?? 0,
            RainIntensity = session?.Weather.RainIntensity ?? 0,
            SessionRemainingSeconds = session is not null
                ? Math.Max(0, session.EndElapsedTime - session.CurrentElapsedTime)
                : 0,
            SessionName = session?.Kind.ToString().ToUpperInvariant() ?? string.Empty,
            OutstandingPenalties = playerStanding?.Penalties ?? 0,
            TireCompound = FormatTireCompound(player),
            VirtualEnergyFraction = Math.Clamp(player.VirtualEnergy, 0, 1),
            SectorTimes = CreateSectorTimes(
                playerStanding,
                currentElapsedTime >= player.LapStartElapsedTime
                    ? currentElapsedTime - player.LapStartElapsedTime
                    : 0),
        };
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

    public static RaceControlWidgetState CreateRaceControl(
        LmuTelemetrySnapshot snapshot)
    {
        if (snapshot.Player is not { } player)
        {
            return new(
                false,
                0,
                "NO DATA",
                "NO DATA",
                "NO DATA",
                "NO DATA",
                "NO DATA",
                "NO IMPACT",
                "NO DATA",
                false,
                false);
        }

        var standing = snapshot.Standings.FirstOrDefault(item => item.IsPlayer);
        var penalties = standing?.Penalties ?? 0;
        var pitState = standing?.PitState ?? LmuPitState.None;
        var damage = player.Damage;
        var damageStatus = damage switch
        {
            { HasCriticalDamage: true } => "CRITICAL",
            { MaximumDentSeverity: >= 2 } => $"HEAVY · {damage.DamagedAreas} AREAS",
            { DamagedAreas: > 0 } => $"DENTED · {damage.DamagedAreas} AREAS",
            _ => "OK",
        };
        var impactStatus = damage is { LastImpactMagnitude: > 0 }
            ? $"{damage.LastImpactMagnitude:0.0} @ {damage.LastImpactElapsedTime:0}s"
            : "NO IMPACT";
        var flag = snapshot.Session?.GamePhase switch
        {
            LmuGamePhase.FullCourseYellow => "FULL COURSE YELLOW",
            LmuGamePhase.Stopped => "RED",
            LmuGamePhase.GreenFlag when standing?.Flag == 6 => "BLUE",
            LmuGamePhase.GreenFlag => "GREEN",
            { } phase => phase.ToString().ToUpperInvariant(),
            _ => "UNKNOWN",
        };
        var requiresAttention =
            penalties > 0 ||
            player.LapInvalidated ||
            damage?.HasCriticalDamage == true ||
            damage?.MaximumDentSeverity >= 2;

        return new(
            true,
            penalties,
            penalties > 0 ? $"{penalties} OUTSTANDING" : "CLEAR",
            pitState == LmuPitState.None
                ? standing?.IsInPits == true ? "PIT LANE" : "TRACK"
                : pitState.ToString().ToUpperInvariant(),
            player.LapInvalidated ? "INVALID" : "VALID",
            flag,
            damageStatus,
            impactStatus,
            $"{(player.SpeedLimiterActive ? "LIMITER ON" : "LIMITER OFF")} · " +
            $"{(standing?.DrsActive == true ? "DRS ON" : "DRS OFF")}",
            requiresAttention,
            damage?.HasCriticalDamage == true);
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
                    ExtractCarNumber(item.VehicleName, item.VehicleModel),
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

    private static string ExtractCarNumber(string vehicleName, string vehicleModel)
    {
        var explicitMatch = System.Text.RegularExpressions.Regex.Match(
            vehicleName,
            @"(?:#|\b(?:CAR|NO|NUM|NUMBER)\s*[:#-]?)\s*(?<number>\d{1,3})",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant |
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (explicitMatch.Success)
        {
            return explicitMatch.Groups["number"].Value;
        }

        var modelNumbers = System.Text.RegularExpressions.Regex.Matches(
                vehicleModel,
                @"(?<!\d)\d{1,4}(?!\d)",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant)
            .Select(match => match.Value)
            .ToHashSet(StringComparer.Ordinal);
        return System.Text.RegularExpressions.Regex.Matches(
                vehicleName,
                @"(?<![A-Za-z0-9])(?<number>\d{1,3})(?![A-Za-z0-9])",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant)
            .Select(match => match.Groups["number"].Value)
            .LastOrDefault(number => !modelNumbers.Contains(number))
            ?? "--";
    }

    public static RelativeWidgetState CreateRelative(
        LmuTelemetrySnapshot snapshot,
        int carsEachSide = 4)
    {
        var player = snapshot.Standings.FirstOrDefault(item => item.IsPlayer) ??
            snapshot.Standings.FirstOrDefault(item =>
                item.VehicleId == snapshot.Player?.VehicleId);
        var lapLength = snapshot.Session?.LapLengthMeters ?? 0;
        if (player is null || !double.IsFinite(lapLength) || lapLength <= 100)
        {
            return new RelativeWidgetState(Array.Empty<RelativeRowState>());
        }

        var referenceLapSeconds = new[]
            {
                player.LastLapTimeSeconds,
                player.BestLapTimeSeconds,
            }
            .FirstOrDefault(value => double.IsFinite(value) && value is > 20 and < 1_800);
        if (referenceLapSeconds <= 0)
        {
            var lapTimes = snapshot.Standings
                .SelectMany(item => new[] { item.LastLapTimeSeconds, item.BestLapTimeSeconds })
                .Where(value => double.IsFinite(value) && value is > 20 and < 1_800)
                .OrderBy(value => value)
                .ToArray();
            referenceLapSeconds = lapTimes.Length == 0
                ? 120
                : lapTimes[lapTimes.Length / 2];
        }

        var metersPerSecond = lapLength / referenceLapSeconds;
        var relative = snapshot.Standings
            .Where(item => item.IsPlayer || !item.IsInGarage)
            .Select(item => new
            {
                Standing = item,
                DistanceMeters = CircularDistance(
                    item.LapDistanceMeters - player.LapDistanceMeters,
                    lapLength),
            })
            .ToArray();
        var count = Math.Max(1, carsEachSide);
        var selected = relative
            .Where(item => item.DistanceMeters > 0)
            .OrderBy(item => item.DistanceMeters)
            .Take(count)
            .Concat(relative.Where(item => item.Standing.VehicleId == player.VehicleId))
            .Concat(relative
                .Where(item => item.DistanceMeters < 0)
                .OrderByDescending(item => item.DistanceMeters)
                .Take(count))
            .OrderByDescending(item => item.DistanceMeters)
            .ToArray();
        var rows = selected
            .Select(entry =>
            {
                var item = entry.Standing;
                return new RelativeRowState(
                item.Position,
                item.DriverName,
                AbbreviateRelativeDriverName(item.DriverName),
                item.VehicleClass,
                AbbreviateVehicleClass(item.VehicleClass),
                ExtractCarNumber(item.VehicleName, item.VehicleModel),
                item.VehicleId == player.VehicleId
                    ? 0
                    : -entry.DistanceMeters / metersPerSecond,
                0,
                item.VehicleId == player.VehicleId,
                item.IsInPits || item.PitState is not LmuPitState.None);
            })
            .ToArray();
        return new RelativeWidgetState(rows);
    }

    private static double CircularDistance(double distanceMeters, double lapLength)
    {
        var halfLap = lapLength / 2;
        if (distanceMeters > halfLap)
        {
            return distanceMeters - lapLength;
        }

        if (distanceMeters < -halfLap)
        {
            return distanceMeters + lapLength;
        }

        return distanceMeters;
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

    private static string FormatTireCompound(LmuPlayerTelemetry player)
    {
        var front = player.FrontTireCompound.Trim();
        var rear = player.RearTireCompound.Trim();
        if (front.Length == 0 && rear.Length == 0)
        {
            return "UNKNOWN";
        }

        return string.Equals(front, rear, StringComparison.OrdinalIgnoreCase) ||
               rear.Length == 0
            ? front
            : front.Length == 0
                ? rear
                : $"{front} / {rear}";
    }

    private static DashboardSectorTimes CreateSectorTimes(
        LmuVehicleStanding? standing,
        double currentLapSeconds)
    {
        if (standing is null)
        {
            return default;
        }

        var currentSector2 = PositiveDifference(
            standing.CurrentSector2CumulativeSeconds,
            standing.CurrentSector1Seconds);
        var currentSector3 = standing.Sector == 0
            ? PositiveDifference(currentLapSeconds, standing.CurrentSector2CumulativeSeconds)
            : 0;
        return new(
            standing.CurrentSector1Seconds,
            currentSector2,
            currentSector3,
            standing.LastSector1Seconds,
            PositiveDifference(
                standing.LastSector2CumulativeSeconds,
                standing.LastSector1Seconds),
            PositiveDifference(
                standing.LastLapTimeSeconds,
                standing.LastSector2CumulativeSeconds),
            standing.BestSector1Seconds,
            PositiveDifference(
                standing.BestSector2CumulativeSeconds,
                standing.BestSector1Seconds),
            PositiveDifference(
                standing.BestLapTimeSeconds,
                standing.BestSector2CumulativeSeconds));
    }

    private static double PositiveDifference(double total, double previous) =>
        total > previous && previous >= 0 ? total - previous : 0;

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
