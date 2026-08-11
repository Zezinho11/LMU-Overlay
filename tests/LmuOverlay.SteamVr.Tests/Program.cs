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
                "DashboardTitle": "VR TEST",
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
}
finally
{
    if (File.Exists(desktopProfilePath)) File.Delete(desktopProfilePath);
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
