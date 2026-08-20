using LmuOverlay.Domain;

namespace LmuOverlay.Widgets;

public static partial class EssentialWidgetStateFactory
{
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
        var sectorTimes = CreateSectorTimes(
            playerStanding,
            currentElapsedTime >= player.LapStartElapsedTime
                ? currentElapsedTime - player.LapStartElapsedTime
                : 0);
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
            // TelemInfoV01.mLapNumber is zero-based while LMU's HUD displays
            // the current lap as a one-based number.
            Math.Max(0, player.LapNumber + 1),
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
            SectorTimes = sectorTimes,
            OptimalLapTimeSeconds = sectorTimes.OptimalLapTimeSeconds,
            VehicleClass = playerStanding?.VehicleClass ?? string.Empty,
            VehicleModel = string.IsNullOrWhiteSpace(player.VehicleModel)
                ? player.VehicleName
                : player.VehicleModel,
        };
    }
}
