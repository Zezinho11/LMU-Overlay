using LmuOverlay.Domain;
using LmuOverlay.SteamVr;
using LmuOverlay.Widgets;

var sanitized = new SteamVrOverlaySettings(
    WidthMeters: 99,
    DistanceMeters: 0,
    VerticalOffsetMeters: 9,
    HorizontalOffsetMeters: -9,
    Opacity: 0).Sanitize();
Require(sanitized.WidthMeters == 3, "VR width is clamped.");
Require(sanitized.DistanceMeters == 0.3f, "VR distance is clamped.");
Require(sanitized.Opacity == 0.1f, "VR opacity is clamped.");

var transform = SteamVrMatrix34.HeadLocked(new(
    DistanceMeters: 1.4f,
    VerticalOffsetMeters: -0.3f,
    HorizontalOffsetMeters: 0.2f));
Require(transform.M0 == 1 && transform.M5 == 1 && transform.M10 == 1,
    "Head-locked transform preserves orientation.");
Require(transform.M3 == 0.2f && transform.M7 == -0.3f && transform.M11 == -1.4f,
    "Head-locked transform applies user offsets in OpenVR coordinates.");
Require(!SteamVrProfile.Compact.LiveStandings.Visible &&
        SteamVrProfile.Compact.Relative.Visible,
    "Compact VR preset keeps only the essential nearby timing surface.");
var calibrated = SteamVrProfile.Compact.Calibrate(1.2f, 1.8f).Sanitize();
Require(Math.Abs(calibrated.Dashboard.WidthMeters - 1.056f) < 0.001f,
    "VR calibration scales panel width consistently.");
Require(calibrated.Dashboard.DistanceMeters == 1.8f &&
        calibrated.Relative.DistanceMeters == 1.8f &&
        calibrated.Inputs.DistanceMeters == 1.8f &&
        calibrated.RaceControl.DistanceMeters == 1.8f &&
        calibrated.PriorityAlert.DistanceMeters == 1.8f,
    "VR calibration applies a consistent readable distance.");

var unavailable = LmuTelemetrySnapshot.Unavailable(
    LmuConnectionState.Disconnected,
    "test");
var texture = VrDashboardTexture.Render(
    EssentialWidgetStateFactory.CreateDashboard(unavailable));
Require(texture.Length == VrDashboardTexture.Width * VrDashboardTexture.Height * 4,
    "VR renderer returns a complete RGBA frame.");
Require(texture.Where((_, index) => index % 4 == 3).Any(alpha => alpha > 0),
    "VR frame contains visible pixels outside its transparent rounded corners.");
Require(
    typeof(VrWidgetTextureRenderer).Assembly.GetManifestResourceNames().Contains(
        "LmuOverlay.SteamVr.Assets.steering-wheel.png",
        StringComparer.Ordinal),
    "SteamVR Inputs must embed the same transparent steering-wheel artwork as Desktop.");

var standingsFrame = VrWidgetTextureRenderer.LiveStandings(
    EssentialWidgetStateFactory.CreateLiveStandings(unavailable));
var relativeFrame = VrWidgetTextureRenderer.Relative(
    EssentialWidgetStateFactory.CreateRelative(unavailable));
var sessionFrame = VrWidgetTextureRenderer.Session(
    EssentialWidgetStateFactory.CreateSessionFlags(unavailable));
var inputsFrame = VrWidgetTextureRenderer.Inputs(
    EssentialWidgetStateFactory.CreateInputs(unavailable));
var fuelFrame = VrWidgetTextureRenderer.Fuel(
    new FuelStrategyTracker().Update(unavailable));
var raceControlFrame = VrWidgetTextureRenderer.RaceControl(
    EssentialWidgetStateFactory.CreateRaceControl(unavailable));
var priorityFrame = VrWidgetTextureRenderer.PriorityAlert(
    EssentialWidgetStateFactory.CreateDashboard(unavailable),
    EssentialWidgetStateFactory.CreateSessionFlags(unavailable),
    new FuelStrategyTracker().Update(unavailable),
    EssentialWidgetStateFactory.CreateRaceControl(unavailable),
    VrRenderStyle.From(new VrDesktopSettings()),
    new VrDesktopSettings());
Require(standingsFrame.Pixels.Length == standingsFrame.Width * standingsFrame.Height * 4,
    "Standings surface returns a complete RGBA frame.");
Require(relativeFrame.Pixels.Length == relativeFrame.Width * relativeFrame.Height * 4,
    "Relative surface returns a complete RGBA frame.");
Require(sessionFrame.Pixels.Length == sessionFrame.Width * sessionFrame.Height * 4,
    "Session surface returns a complete RGBA frame.");
Require(inputsFrame.Pixels.Length == inputsFrame.Width * inputsFrame.Height * 4,
    "Inputs surface returns a complete RGBA frame.");
Require(fuelFrame.Pixels.Length == fuelFrame.Width * fuelFrame.Height * 4,
    "Fuel strategy surface returns a complete RGBA frame.");
Require(raceControlFrame.Pixels.Length == raceControlFrame.Width * raceControlFrame.Height * 4,
    "Race Control surface returns a complete RGBA frame.");
Require(priorityFrame.Pixels.Length == priorityFrame.Width * priorityFrame.Height * 4,
    "Priority-alert surface returns a complete transparent-or-visible RGBA frame.");

var profilePath = System.IO.Path.Combine(
    System.IO.Path.GetTempPath(),
    $"lmu-overlay-vr-{Guid.NewGuid():N}.json");
try
{
    var store = new SteamVrProfileStore(profilePath);
    var profile = store.Load();
    Require(File.Exists(profilePath), "First run creates a persistent VR profile.");
    Require(profile.Dashboard.Visible && profile.LiveStandings.Visible &&
            profile.Inputs.Visible && profile.RaceControl.Visible,
        "Default VR profile enables every desktop-equivalent surface.");
    var changed = profile with
    {
        Relative = profile.Relative with { Visible = false, WidthMeters = 99 },
    };
    store.Save(changed);
    var reloaded = store.Load();
    Require(!reloaded.Relative.Visible, "VR visibility persists.");
    Require(reloaded.Relative.WidthMeters == 3, "VR profile values are sanitized on save.");
    Require(reloaded.Inputs.Visible && reloaded.RaceControl.Visible &&
            reloaded.PriorityAlert.Visible,
        "The additional VR placements persist through profile round-trips.");
}
finally
{
    if (File.Exists(profilePath))
    {
        File.Delete(profilePath);
    }
}

var desktopProfilePath = System.IO.Path.Combine(
    System.IO.Path.GetTempPath(),
    $"lmu-overlay-desktop-profile-{Guid.NewGuid():N}.json");
try
{
    File.WriteAllText(desktopProfilePath, """
        {
          "ActiveProfile": "VR Race",
          "Profiles": {
            "VR Race": {
              "Settings": {
                "Theme": "Custom",
                "CustomAccentColor": "#FF2200",
                "CustomBackgroundColor": "#050607",
                "CustomCardColor": "#112233",
                "CustomInformationColor": "#22CCEE",
                "CustomAttentionColor": "#EEAA22",
                "CustomCriticalColor": "#EE2244",
                "CustomPositiveColor": "#33DD88",
                "DashboardTitle": "VR TEST",
                "DashboardShowSectors": false,
                "DashboardShowTires": true,
                "DashboardShowTelemetry": false,
                "RefreshRateHz": 120,
                "LiveStandingsMaximumRows": 10,
                "RelativeCarsEachSide": 5
              }
            }
          }
        }
        """);
    var settings = new DesktopProfileSettingsReader(desktopProfilePath).Load();
    Require(settings.Theme == "Custom" && settings.DashboardTitle == "VR TEST",
        "SteamVR follows the active desktop visual profile.");
    Require(settings.RefreshRateHz == 120 && settings.RelativeCarsEachSide == 5,
        "SteamVR follows desktop update and timing-density settings.");
    var customStyle = VrRenderStyle.From(settings);
    Require(customStyle.Card.R == 17 && customStyle.Card.G == 34 && customStyle.Card.B == 51 &&
            customStyle.Information.R == 34 && customStyle.Information.G == 204 &&
            customStyle.Critical.B == 68,
        "SteamVR must resolve the complete Desktop custom palette.");
    Require(!customStyle.DashboardShowSectors && customStyle.DashboardShowTires &&
            !customStyle.DashboardShowTelemetry,
        "SteamVR must follow the Desktop dashboard module composition.");
    var accessibleSettings = settings with { Theme = "ColorVisionSafe" };
    var accessibleStyle = VrRenderStyle.From(accessibleSettings);
    Require(accessibleStyle.Information != accessibleStyle.Attention &&
            accessibleStyle.Attention != accessibleStyle.Critical &&
            accessibleStyle.Critical != accessibleStyle.Positive,
        "SteamVR uses the same distinct color-vision-safe semantic palette.");
}
finally
{
    if (File.Exists(desktopProfilePath)) File.Delete(desktopProfilePath);
}

var diagnosticsPath = System.IO.Path.Combine(
    System.IO.Path.GetTempPath(),
    $"lmu-overlay-vr-diagnostics-{Guid.NewGuid():N}.json");
try
{
    var diagnosticHealth = new LmuOverlay.Core.TelemetryRuntimeHealth(
        100, 1, 2, 0.2, 0.3, 1.2, DateTimeOffset.UtcNow, string.Empty)
    {
        P99ReadMilliseconds = 0.9,
        StaleAgeMilliseconds = 8,
    };
    Require(SteamVrDiagnosticsWriter.TryWrite(
            diagnosticsPath,
            diagnosticHealth,
            new(true, 3, DateTimeOffset.UtcNow, string.Empty),
            SteamVrProfile.Default),
        "SteamVR must export privacy-safe runtime diagnostics.");
    var diagnostics = File.ReadAllText(diagnosticsPath);
    Require(diagnostics.Contains("P99ReadMilliseconds", StringComparison.Ordinal) &&
            diagnostics.Contains("RecoveryAttempts", StringComparison.Ordinal),
        "SteamVR diagnostics must include latency and compositor recovery health.");
    Require(!diagnostics.Contains("Spa", StringComparison.OrdinalIgnoreCase) &&
            !diagnostics.Contains("Circuit", StringComparison.OrdinalIgnoreCase),
        "SteamVR diagnostics must omit track and driver identities.");
}
finally
{
    if (File.Exists(diagnosticsPath)) File.Delete(diagnosticsPath);
}

var baselinePath = System.IO.Path.Combine(
    System.IO.Path.GetTempPath(),
    $"lmu-overlay-vr-baselines-{Guid.NewGuid():N}");
try
{
    var baselines = VrVisualBaselineWriter.Write(baselinePath);
    Require(baselines.Count == 9 && baselines.All(File.Exists),
        "VR visual qualification must export all eight surfaces and the HMD composition.");
    Require(baselines.All(path => new FileInfo(path).Length > 1_024),
        "VR visual baselines must contain rendered PNG data.");
}
finally
{
    if (Directory.Exists(baselinePath)) Directory.Delete(baselinePath, true);
}

Console.WriteLine("SteamVR foundation checks passed.");
return 0;

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Check failed: {message}");
    }
}
