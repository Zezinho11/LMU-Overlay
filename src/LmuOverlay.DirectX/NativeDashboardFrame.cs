using LmuOverlay.Widgets;

namespace LmuOverlay.DirectX;

public readonly record struct NativeDashboardBounds(
    int Left,
    int Top,
    int Width,
    int Height);

public sealed record NativeDashboardFrame(
    DashboardWidgetState Dashboard,
    NativeDashboardBounds Bounds,
    bool Visible,
    long Sequence,
    long CapturedTimestamp);
