using LmuOverlay.Core;
using LmuOverlay.Desktop;
using LmuOverlay.SteamVr;

Require(
    DesktopPresentationFeatures.Supported == PresentationFeatureSet.Required,
    "Desktop must implement every required presentation feature.");
Require(
    SteamVrPresentationFeatures.Supported == PresentationFeatureSet.Required,
    "SteamVR must implement every required presentation feature.");
Require(
    DesktopPresentationFeatures.Supported == SteamVrPresentationFeatures.Supported,
    "Desktop and SteamVR feature manifests must remain identical.");

Require(PresentationRecoveryPolicy.DelayForFailure(1) == TimeSpan.FromMilliseconds(250),
    "Recovery starts quickly.");
Require(PresentationRecoveryPolicy.DelayForFailure(5) == TimeSpan.FromSeconds(4),
    "Recovery backoff is bounded at four seconds.");
Require(PresentationRecoveryPolicy.DelayForFailure(50) == TimeSpan.FromSeconds(4),
    "Recovery never leaves either host unavailable for an unbounded interval.");

Console.WriteLine("Desktop/SteamVR presentation parity checks passed.");
return 0;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException($"Check failed: {message}");
}
