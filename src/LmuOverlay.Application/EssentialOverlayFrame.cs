using LmuOverlay.Domain;
using LmuOverlay.Widgets;

namespace LmuOverlay.Application;

/// <summary>
/// Presentation-neutral state shared by the desktop and SteamVR hosts.
/// Stateful timing and strategy trackers remain independent services and are
/// deliberately not coupled to a renderer.
/// </summary>
public sealed record EssentialOverlayFrame(
    DashboardWidgetState Dashboard,
    InputsWidgetState Inputs,
    SessionFlagsWidgetState SessionFlags,
    RaceControlWidgetState RaceControl);

public sealed class EssentialOverlayFrameComposer
{
    public EssentialOverlayFrame Compose(LmuTelemetrySnapshot snapshot) => new(
        EssentialWidgetStateFactory.CreateDashboard(snapshot),
        EssentialWidgetStateFactory.CreateInputs(snapshot),
        EssentialWidgetStateFactory.CreateSessionFlags(snapshot),
        EssentialWidgetStateFactory.CreateRaceControl(snapshot));
}
