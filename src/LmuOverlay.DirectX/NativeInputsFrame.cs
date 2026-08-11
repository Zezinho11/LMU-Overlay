using LmuOverlay.Widgets;

namespace LmuOverlay.DirectX;

public readonly record struct NativeInputsFrame(
    InputsWidgetState Inputs,
    NativeDashboardBounds Bounds,
    bool Visible,
    long Sequence,
    long CapturedTimestamp,
    string SessionKey = "",
    NativeOverlayStyle? Style = null);
