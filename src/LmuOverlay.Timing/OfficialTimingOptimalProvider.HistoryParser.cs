using System.Globalization;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using LmuOverlay.Domain;

namespace LmuOverlay.Widgets;

public sealed partial class OfficialTimingOptimalProvider
{
    public static double ParseOptimal(JsonElement history, int vehicleId)
        => ParseOptimal(history, vehicleId, excludedLapSignatures: null);

    public static bool HasSupportedSchema(JsonElement history, int vehicleId)
    {
        if (history.ValueKind != JsonValueKind.Object ||
            !TryGetVehicleHistory(history, vehicleId, out var laps) ||
            laps.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var lap in laps.EnumerateArray())
        {
            if (lap.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (HasNumber(lap, "sectorTime1") &&
                HasNumber(lap, "sectorTime2") &&
                HasNumber(lap, "lapTime"))
            {
                return true;
            }
        }

        // An empty history is valid at the beginning of a session.
        return laps.GetArrayLength() == 0;
    }

    public static bool IsNewSession(
        bool hasPreviousSession,
        string previousTrackName,
        int previousSessionCode,
        int previousVehicleId,
        double previousElapsedTime,
        string trackName,
        int sessionCode,
        int vehicleId,
        double elapsedTime)
    {
        if (!hasPreviousSession)
        {
            return false;
        }

        var sameIdentity = previousSessionCode == sessionCode &&
            previousVehicleId == vehicleId &&
            string.Equals(previousTrackName, trackName, StringComparison.Ordinal);
        return !sameIdentity || elapsedTime + 1 < previousElapsedTime;
    }

    public static double ParseOptimal(
        JsonElement history,
        int vehicleId,
        IReadOnlySet<string>? excludedLapSignatures)
    {
        if (history.ValueKind != JsonValueKind.Object ||
            !TryGetVehicleHistory(history, vehicleId, out var laps) ||
            laps.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var best1 = double.PositiveInfinity;
        var best2 = double.PositiveInfinity;
        var best3 = double.PositiveInfinity;
        foreach (var lap in laps.EnumerateArray())
        {
            if (excludedLapSignatures?.Contains(lap.GetRawText()) == true)
            {
                continue;
            }
            if (IsExplicitlyInvalid(lap))
            {
                continue;
            }
            var sector1 = Number(lap, "sectorTime1");
            var sector2Cumulative = Number(lap, "sectorTime2");
            var lapTime = Number(lap, "lapTime");
            if (Valid(sector1)) best1 = Math.Min(best1, sector1);
            var sector2 = sector2Cumulative - sector1;
            if (Valid(sector1) && Valid(sector2Cumulative) && Valid(sector2))
            {
                best2 = Math.Min(best2, sector2);
            }
            var sector3 = lapTime - sector2Cumulative;
            if (Valid(sector2Cumulative) && Valid(lapTime) && Valid(sector3))
            {
                best3 = Math.Min(best3, sector3);
            }
        }

        return double.IsFinite(best1) && double.IsFinite(best2) && double.IsFinite(best3)
            ? best1 + best2 + best3
            : 0;
    }

    private static HashSet<string> CaptureLapSignatures(
        JsonElement history,
        int vehicleId)
    {
        if (history.ValueKind != JsonValueKind.Object ||
            !TryGetVehicleHistory(history, vehicleId, out var laps) ||
            laps.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return laps.EnumerateArray()
            .Select(lap => lap.GetRawText())
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool IsExplicitlyInvalid(JsonElement lap)
    {
        foreach (var property in lap.EnumerateObject())
        {
            if ((property.Name.Equals("valid", StringComparison.OrdinalIgnoreCase) ||
                 property.Name.Equals("isValid", StringComparison.OrdinalIgnoreCase) ||
                 property.Name.Equals("lapValid", StringComparison.OrdinalIgnoreCase) ||
                 property.Name.Equals("isLapValid", StringComparison.OrdinalIgnoreCase)) &&
                property.Value.ValueKind is JsonValueKind.False)
            {
                return true;
            }

            if ((property.Name.Equals("invalid", StringComparison.OrdinalIgnoreCase) ||
                 property.Name.Equals("isInvalid", StringComparison.OrdinalIgnoreCase) ||
                 property.Name.Equals("lapInvalid", StringComparison.OrdinalIgnoreCase) ||
                 property.Name.Equals("lapInvalidated", StringComparison.OrdinalIgnoreCase)) &&
                property.Value.ValueKind is JsonValueKind.True)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetVehicleHistory(
        JsonElement history,
        int vehicleId,
        out JsonElement laps)
    {
        var key = vehicleId.ToString(CultureInfo.InvariantCulture);
        if (history.TryGetProperty(key, out laps))
        {
            return true;
        }

        laps = default;
        return false;
    }

    private static double Number(JsonElement value, string name)
    {
        foreach (var property in value.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) &&
                property.Value.TryGetDouble(out var number))
            {
                return number;
            }
        }
        return 0;
    }

    private static bool HasNumber(JsonElement value, string name)
    {
        foreach (var property in value.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.Number &&
                property.Value.TryGetDouble(out var number) &&
                double.IsFinite(number))
            {
                return true;
            }
        }
        return false;
    }
}
