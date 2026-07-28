namespace LmuOverlay.Domain;

public sealed record LmuTelemetryMetrics(
    double? SessionTimeRemainingSeconds,
    int? LapsRemaining,
    double? CurrentLapTimeSeconds,
    double? LapProgress,
    double? FuelFraction,
    double? EngineRpmFraction,
    double? BatteryFraction,
    double SpeedKilometersPerHour)
{
    public static LmuTelemetryMetrics Empty { get; } =
        new(null, null, null, null, null, null, null, 0);
}

public static class LmuTelemetryMetricsCalculator
{
    public static LmuTelemetryMetrics Calculate(LmuTelemetrySnapshot snapshot)
    {
        if (snapshot.State != LmuConnectionState.Connected ||
            snapshot.Session is null ||
            snapshot.Player is null)
        {
            return LmuTelemetryMetrics.Empty;
        }

        var session = snapshot.Session;
        var player = snapshot.Player;

        return new(
            session.EndElapsedTime > session.CurrentElapsedTime
                ? session.EndElapsedTime - session.CurrentElapsedTime
                : null,
            LmuSessionLimits.HasFiniteLapLimit(session.MaximumLaps)
                ? Math.Max(0, session.MaximumLaps - player.LapNumber)
                : null,
            session.CurrentElapsedTime >= player.LapStartElapsedTime
                ? session.CurrentElapsedTime - player.LapStartElapsedTime
                : null,
            session.LapLengthMeters > 0
                ? Clamp01(player.LapDistanceMeters / session.LapLengthMeters)
                : null,
            player.FuelCapacityLiters > 0
                ? Clamp01(player.FuelLiters / player.FuelCapacityLiters)
                : null,
            player.EngineMaximumRpm > 0
                ? Clamp01(player.EngineRpm / player.EngineMaximumRpm)
                : null,
            Clamp01(Math.Max(player.BatteryChargeFraction, player.StateOfCharge)),
            player.SpeedKilometersPerHour);
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0, 1);
}
