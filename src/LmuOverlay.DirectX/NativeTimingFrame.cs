using LmuOverlay.Widgets;

namespace LmuOverlay.DirectX;

public sealed record NativeTimingFrame(
    LiveStandingsWidgetState LiveStandings,
    NativeDashboardBounds LiveStandingsBounds,
    bool LiveStandingsVisible,
    double LiveStandingsOpacity,
    RelativeWidgetState Relative,
    NativeDashboardBounds RelativeBounds,
    bool RelativeVisible,
    double RelativeOpacity,
    long Sequence,
    NativeOverlayStyle? Style = null);
