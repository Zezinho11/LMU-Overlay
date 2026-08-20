using LmuOverlay.Core;

namespace LmuOverlay.SteamVr;

public enum VrBackendKind
{
    SteamVr,
    OpenXrExperimental,
}

public sealed record VrBackendDecision(
    VrBackendKind Requested,
    VrBackendKind Selected,
    bool FellBack,
    bool CanStart,
    VrRuntimeReport Runtime,
    string Detail);

public static class VrBackendSelector
{
    public static VrBackendDecision Select(
        string? requested,
        bool allowSteamVrFallback = true,
        VrRuntimeReport? runtime = null)
    {
        runtime ??= VrRuntimeProbe.Detect();
        var wantsOpenXr = string.Equals(requested, "openxr", StringComparison.OrdinalIgnoreCase);
        if (!wantsOpenXr)
        {
            return new(VrBackendKind.SteamVr, VrBackendKind.SteamVr, false,
                runtime.SteamVrInstalled, runtime,
                runtime.SteamVrInstalled ? runtime.Detail : "SteamVR is not installed.");
        }

        // OpenXR core composition layers belong to the application's own session.
        // This external overlay deliberately does not install or inject an API layer
        // into LMU. The isolated backend remains capability-gated until a runtime
        // offers a documented, EAC-safe external overlay path.
        if (allowSteamVrFallback && runtime.SteamVrInstalled)
        {
            return new(VrBackendKind.OpenXrExperimental, VrBackendKind.SteamVr, true,
                true, runtime,
                "OpenXR external overlay is unavailable safely; falling back to SteamVR IVROverlay.");
        }

        return new(VrBackendKind.OpenXrExperimental, VrBackendKind.OpenXrExperimental,
            false, false, runtime,
            "OpenXR runtime detected, but no EAC-safe external overlay extension is available.");
    }
}
