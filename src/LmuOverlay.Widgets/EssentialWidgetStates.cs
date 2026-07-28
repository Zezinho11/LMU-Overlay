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
    string TrackName);

public sealed record InputsWidgetState(
    bool Available,
    double Throttle,
    double Brake,
    double Clutch,
    double Steering,
    bool AbsActive,
    bool TractionControlActive);

public static class EssentialWidgetStateFactory
{
    public static DashboardWidgetState CreateDashboard(LmuTelemetrySnapshot snapshot)
    {
        if (snapshot.Player is not { } player)
        {
            return new(false, 0, "N", 0, 0, 0, 0, 0, snapshot.Session?.TrackName ?? string.Empty);
        }

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
            snapshot.Session?.TrackName ?? string.Empty);
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

    private static string FormatGear(int gear) => gear switch
    {
        < 0 => "R",
        0 => "N",
        _ => gear.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    private static double ClampInput(double value) => Math.Clamp(value, 0, 1);
}
