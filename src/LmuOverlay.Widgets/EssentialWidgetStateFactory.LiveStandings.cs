using LmuOverlay.Domain;

namespace LmuOverlay.Widgets;

public static partial class EssentialWidgetStateFactory
{
    public static LiveStandingsWidgetState CreateLiveStandings(
        LmuTelemetrySnapshot snapshot,
        int maximumRows = 12)
    {
        var isQualifying = snapshot.Session?.Kind == LmuSessionKind.Qualifying;
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
        var visibleCarCapacity = Math.Min(
            Math.Clamp(maximumRows, 6, 12),
            Math.Max(
                2,
                (LiveStandingsContentHeight -
                 (selectedGroups.Length * LiveStandingsClassHeaderHeight)) /
                LiveStandingsRowHeight));
        var playerClassLimit = Math.Max(2, visibleCarCapacity - otherClassCount);

        var classes = selectedGroups
            .Select(group =>
            {
                var isPlayerClass = string.Equals(
                    group.Key,
                    playerClass,
                    StringComparison.OrdinalIgnoreCase);
                var classOrder = group.OrderBy(item => item.Position).ToArray();
                var classBestLap = classOrder
                    .Select(item => item.BestLapTimeSeconds)
                    .Where(value => value > 0 && double.IsFinite(value))
                    .DefaultIfEmpty(0)
                    .Min();
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
                    isQualifying && classBestLap > 0 && item.BestLapTimeSeconds > 0
                        ? Math.Max(0, item.BestLapTimeSeconds - classBestLap)
                        : item.GapToNextSeconds,
                    isQualifying ? 0 : item.LapsBehindNext,
                    isQualifying ? item.BestLapTimeSeconds : item.LastLapTimeSeconds,
                    item.BestLapTimeSeconds,
                    item.IsPlayer,
                    item.IsInPits || item.PitState is not LmuPitState.None,
                    isQualifying,
                    NormalizeVirtualEnergy(item.VirtualEnergyFraction),
                    FormatStandingTireCompound(item),
                    item.FrontTireCompoundIndex))
                    .ToArray();
                return new LiveStandingsClassState(
                    group.Key,
                    isPlayerClass,
                    rows);
            })
            .OrderByDescending(item => item.IsPlayerClass)
            .ThenBy(item => item.ClassName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new LiveStandingsWidgetState(
            playerClass,
            classes,
            snapshot.Session is { } session
                ? FormatSessionKind(session.Kind)
                : "SESSION",
            SessionRemaining(snapshot.Session),
            isQualifying);
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
}
