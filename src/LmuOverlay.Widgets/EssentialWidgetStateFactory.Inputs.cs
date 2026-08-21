using LmuOverlay.Domain;

namespace LmuOverlay.Widgets;

public static partial class EssentialWidgetStateFactory
{
    public static InputsWidgetState CreateInputs(
        LmuTelemetrySnapshot snapshot,
        double steeringWheelRangeDegrees = 0,
        double? directSteeringPosition = null)
    {
        if (snapshot.Player is not { } player)
        {
            return new(false, 0, 0, 0, 0, false, false, 0);
        }

        return new(
            true,
            ClampInput(player.Throttle),
            ClampInput(player.Brake),
            ClampInput(player.Clutch),
            Math.Clamp(directSteeringPosition ?? player.Steering, -1, 1),
            player.AbsActive,
            player.TractionControlActive,
            directSteeringPosition.HasValue
                ? SteeringWheelRotation.ResolveSynchronizedRangeDegrees(
                    directSteeringPosition.Value,
                    player.Steering,
                    player.VisualSteeringWheelRangeDegrees,
                    player.PhysicalSteeringWheelRangeDegrees,
                    steeringWheelRangeDegrees)
                : SteeringWheelRotation.ResolvePhysicalRangeDegrees(
                    player.PhysicalSteeringWheelRangeDegrees,
                    player.VisualSteeringWheelRangeDegrees,
                    steeringWheelRangeDegrees));
    }
}
