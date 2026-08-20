using LmuOverlay.Domain;
using LmuOverlay.Strategy.Learning;
using LmuOverlay.Strategy.Planning;

namespace LmuOverlay.Widgets;

public sealed partial class FuelStrategyTracker
{
    private bool HasSessionChanged(
        LmuSessionSnapshot session,
        int completedLaps,
        string vehicleIdentity,
        int gameVersion) =>
        !string.Equals(_trackName, session.TrackName, StringComparison.Ordinal) ||
        !string.Equals(_vehicleIdentity, vehicleIdentity, StringComparison.Ordinal) ||
        _gameVersion != gameVersion ||
        _sessionCode != session.SessionCode ||
        completedLaps < _lastCompletedLaps;

    private void Reset(
        LmuSessionSnapshot session,
        int completedLaps,
        double fuelLiters,
        double virtualEnergy,
        LmuWheelWear tireWear,
        string vehicleIdentity,
        int gameVersion)
    {
        _learning.Reset(fuelLiters, virtualEnergy, tireWear);
        _rainSamples.Clear();
        _trackName = session.TrackName;
        _vehicleIdentity = vehicleIdentity;
        _gameVersion = gameVersion;
        _sessionCode = session.SessionCode;
        _lastCompletedLaps = completedLaps;
        _lapStartFuel = fuelLiters;
        _previousFuel = fuelLiters;
        _lapStartVirtualEnergy = virtualEnergy;
        _previousVirtualEnergy = virtualEnergy;
        _lastRainSampleAt = DateTimeOffset.MinValue;
        _previousSampleAt = DateTimeOffset.MinValue;
        _lapContaminated = completedLaps == 0;
        _lapStartRain = session.Weather.RainIntensity;
        _scenarioCompletedLaps = int.MinValue;
        _scenario = new(false, 0, 0, 0, 0, 0, "SCENARIOS · LEARNING");
    }

    private static bool IsInPitLane(LmuVehicleStanding? standing) =>
        standing?.IsInPits == true ||
        standing?.PitState == LmuPitState.Entering ||
        standing?.PitState == LmuPitState.Stopped;

    private static bool IsPlausibleFuelSample(
        double consumed,
        double capacity,
        IEnumerable<double> samples)
    {
        var maximum = capacity > 0
            ? Math.Clamp(capacity * 0.25, 5, 20)
            : 20;
        if (!double.IsFinite(consumed) || consumed is <= 0.05 || consumed > maximum)
        {
            return false;
        }

        return IsConsistentWithHistory(consumed, samples);
    }

    private static bool IsPlausibleEnergySample(
        double consumed,
        IEnumerable<double> samples) =>
        double.IsFinite(consumed) &&
        consumed is > 0.0001 and <= 1 &&
        IsConsistentWithHistory(consumed, samples);

    private static bool IsConsistentWithHistory(
        double value,
        IEnumerable<double> samples)
    {
        var history = samples.OrderBy(sample => sample).ToArray();
        if (history.Length < 2)
        {
            return true;
        }

        var median = history.Length % 2 == 0
            ? (history[history.Length / 2 - 1] + history[history.Length / 2]) / 2
            : history[history.Length / 2];
        return median <= 0 || value >= median * 0.45 && value <= median * 1.8;
    }

    private void CaptureRainSample(double rainIntensity, DateTimeOffset capturedAt)
    {
        if (_lastRainSampleAt != DateTimeOffset.MinValue &&
            capturedAt - _lastRainSampleAt < TimeSpan.FromSeconds(1))
        {
            return;
        }

        _rainSamples.Add(Math.Clamp(rainIntensity, 0, 1));

        _lastRainSampleAt = capturedAt;
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
                : $"{front}/{rear}";
    }

    private static LmuWheelWear TireLifeToConsumedWear(LmuWheelWear remainingLife) => new(
        Math.Clamp(1 - remainingLife.FrontLeftFraction, 0, 1),
        Math.Clamp(1 - remainingLife.FrontRightFraction, 0, 1),
        Math.Clamp(1 - remainingLife.RearLeftFraction, 0, 1),
        Math.Clamp(1 - remainingLife.RearRightFraction, 0, 1));

    private static double MaximumTireWear(LmuWheelWear remainingLife)
    {
        var consumed = TireLifeToConsumedWear(remainingLife);
        return Math.Max(
            Math.Max(consumed.FrontLeftFraction, consumed.FrontRightFraction),
            Math.Max(consumed.RearLeftFraction, consumed.RearRightFraction));
    }

    private static double NormalizeVirtualEnergy(double value) =>
        double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;

    private static string Confidence(int samples) => samples switch
    {
        >= 6 => "HIGH",
        >= 3 => "MEDIUM",
        > 0 => "LOW",
        _ => "LEARNING",
    };

    private static FuelStrategyWidgetState Unavailable() => new(
        false, true, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, "LEARNING",
        "NO DATA");
}
