using LmuOverlay.Domain;

namespace LmuOverlay.Widgets;

/// <summary>
/// Converts each car's rev limit into a shift-light window and refines the
/// target independently per vehicle and gear from clean full-throttle shifts.
/// LMU publishes RPM and maximum RPM but no native dashboard-lamp bitfield.
/// </summary>
public sealed class ShiftLightTimingTracker
{
    private readonly Dictionary<string, Dictionary<int, LearnedTarget>> _targets =
        new(StringComparer.OrdinalIgnoreCase);
    private string _lastVehicle = string.Empty;
    private int _lastGear;
    private double _lastRpm;
    private double _lastMaximumRpm;
    private double _lastThrottle;

    public double Update(LmuPlayerTelemetry? player)
    {
        if (player is null || player.EngineRpm <= 0 || player.EngineMaximumRpm <= 0)
        {
            ResetTransient();
            return 0;
        }

        var vehicle = VehicleKey(player);
        if (!string.Equals(vehicle, _lastVehicle, StringComparison.OrdinalIgnoreCase))
        {
            ResetTransient();
            _lastVehicle = vehicle;
        }

        if (_lastGear > 0 && player.Gear == _lastGear + 1 &&
            _lastThrottle >= 0.72 && _lastMaximumRpm > 0 &&
            _lastRpm >= _lastMaximumRpm * 0.72)
        {
            Observe(vehicle, _lastGear, _lastRpm, _lastMaximumRpm);
        }

        var target = TargetRpm(vehicle, Math.Max(1, player.Gear), player.EngineMaximumRpm);
        var start = target * StartFraction(vehicle);
        var fraction = target > start
            ? Math.Clamp((player.EngineRpm - start) / (target - start), 0, 1)
            : 0;

        _lastGear = player.Gear;
        _lastRpm = player.EngineRpm;
        _lastMaximumRpm = player.EngineMaximumRpm;
        _lastThrottle = player.Throttle;
        return fraction;
    }

    private void Observe(string vehicle, int gear, double rpm, double maximumRpm)
    {
        var ratio = Math.Clamp(rpm / maximumRpm, 0.76, 0.995);
        if (!_targets.TryGetValue(vehicle, out var gears))
        {
            gears = [];
            _targets[vehicle] = gears;
        }

        if (!gears.TryGetValue(gear, out var learned))
        {
            gears[gear] = new(ratio, 1);
            return;
        }

        var weight = learned.Samples < 3 ? 0.35 : 0.15;
        gears[gear] = new(
            learned.Ratio + (ratio - learned.Ratio) * weight,
            Math.Min(1000, learned.Samples + 1));
    }

    private double TargetRpm(string vehicle, int gear, double maximumRpm)
    {
        if (_targets.TryGetValue(vehicle, out var gears) &&
            gears.TryGetValue(gear, out var learned))
        {
            return maximumRpm * learned.Ratio;
        }
        return maximumRpm * DefaultTargetFraction(vehicle);
    }

    private static string VehicleKey(LmuPlayerTelemetry player) =>
        string.IsNullOrWhiteSpace(player.VehicleModel)
            ? player.VehicleName.Trim()
            : player.VehicleModel.Trim();

    private static double DefaultTargetFraction(string vehicle)
    {
        if (Contains(vehicle, "LEXUS")) return 0.93;
        if (Contains(vehicle, "BMW") || Contains(vehicle, "ASTON")) return 0.95;
        if (Contains(vehicle, "FORD") || Contains(vehicle, "CADILLAC")) return 0.955;
        if (Contains(vehicle, "MERCEDES") || Contains(vehicle, "CORVETTE")) return 0.96;
        if (Contains(vehicle, "FERRARI") || Contains(vehicle, "MCLAREN")) return 0.97;
        if (Contains(vehicle, "PORSCHE")) return 0.98;
        return 0.965;
    }

    private static double StartFraction(string vehicle)
    {
        return Contains(vehicle, "PORSCHE") || Contains(vehicle, "FERRARI")
            ? 0.80
            : 0.82;
    }

    private static bool Contains(string value, string token) =>
        value.Contains(token, StringComparison.OrdinalIgnoreCase);

    private void ResetTransient()
    {
        _lastVehicle = string.Empty;
        _lastGear = 0;
        _lastRpm = 0;
        _lastMaximumRpm = 0;
        _lastThrottle = 0;
    }

    private readonly record struct LearnedTarget(double Ratio, int Samples);
}
