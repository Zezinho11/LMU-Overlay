using LmuOverlay.Core;
using LmuOverlay.Desktop;
using LmuOverlay.SteamVr;
using LmuOverlay.Widgets;

Require(
    DesktopPresentationFeatures.Supported == PresentationFeatureSet.Required,
    "Desktop must implement every required presentation feature.");
Require(
    SteamVrPresentationFeatures.Supported == PresentationFeatureSet.Required,
    "SteamVR must implement every required presentation feature.");
Require(
    DesktopPresentationFeatures.Supported == SteamVrPresentationFeatures.Supported,
    "Desktop and SteamVR feature manifests must remain identical.");
Require(
    DesktopPresentationFeatures.Supported.HasFlag(PresentationFeatures.Localization),
    "Desktop and SteamVR parity must include localization.");
Require(OverlayText.Get("pt-BR", OverlayTextKey.FuelAndEnergy) == "Combustível e energia virtual",
    "The shared catalog must expose Brazilian Portuguese.");
Require(OverlayText.Get("en-US", OverlayTextKey.FuelAndEnergy) == "Fuel & virtual energy",
    "The shared catalog must expose English.");
Require(OverlayText.Normalize("unsupported") == OverlayText.PortugueseBrazil,
    "Unknown language values must migrate safely to Brazilian Portuguese.");
Require(new VrDesktopSettings { Language = "en-US" }.Sanitize().Language == OverlayText.EnglishUnitedStates,
    "SteamVR must preserve the same supported language used by Desktop.");

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
