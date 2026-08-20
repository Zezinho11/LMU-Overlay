using LmuOverlay.Domain;

namespace LmuOverlay.Widgets;

public static partial class EssentialWidgetStateFactory
{
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
}
