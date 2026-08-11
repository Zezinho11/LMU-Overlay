using System.Diagnostics;
using LmuOverlay.Core;
using LmuOverlay.Domain;
using LmuOverlay.LmuSharedMemory;
using LmuOverlay.SteamVr;
using LmuOverlay.Widgets;

const string dashboardKey = "com.redfoxracing.lmuoverlay.dashboard";
const string inputsKey = "com.redfoxracing.lmuoverlay.inputs";
const string standingsKey = "com.redfoxracing.lmuoverlay.standings";
const string relativeKey = "com.redfoxracing.lmuoverlay.relative";
const string fuelKey = "com.redfoxracing.lmuoverlay.fuel";
const string sessionKey = "com.redfoxracing.lmuoverlay.session";
const string raceControlKey = "com.redfoxracing.lmuoverlay.racecontrol";
const string priorityAlertKey = "com.redfoxracing.lmuoverlay.priorityalert";

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("The SteamVR host requires Windows.");
    return 2;
}

var profileStore = new SteamVrProfileStore();
var desktopSettingsReader = new DesktopProfileSettingsReader();
var diagnosticsPath = ArgumentValue(args, "--diagnostics");
if (args.Contains("--configure", StringComparer.OrdinalIgnoreCase) ||
    args.Contains("--configure-vr", StringComparer.OrdinalIgnoreCase))
{
    ApplicationConfiguration.Initialize();
    Application.Run(new SteamVrConfigurationForm(profileStore));
    return 0;
}
var profile = ApplyPreset(profileStore.Load(), args);
profileStore.Save(profile);
if (args.Contains("--calibrate", StringComparer.OrdinalIgnoreCase))
{
    profile = Calibrate(profile).Sanitize();
    profileStore.Save(profile);
    Console.WriteLine("SteamVR calibration saved.");
}

await using var telemetry = new TelemetryRuntime(
    () => new LmuSharedMemoryReader(),
    TimeSpan.FromMilliseconds(8),
    TimeSpan.FromSeconds(1));
telemetry.Start();
using var officialOptimal = new OfficialTimingOptimalProvider();
var sectors = new PersistentSectorReferenceTracker(
    new SectorReferenceStore(),
    new PersonalBestLapStore());
var fuelTracker = new FuelStrategyTracker();
var pedalHistory = new Queue<VrPedalSample>();
var pedalSession = string.Empty;

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

Console.WriteLine("SteamVR overlay host started. Press Ctrl+C to close it.");
var vrConsecutiveFailures = 0;
var vrRecoveryAttempts = 0L;
var vrLastRecoveredAt = (DateTimeOffset?)null;
while (!shutdown.IsCancellationRequested)
{
    if (!OpenVrNative.TryConnect(out var openVr, out var detail) || openVr is null)
    {
        vrConsecutiveFailures++;
        vrRecoveryAttempts++;
        var delay = PresentationRecoveryPolicy.DelayForFailure(vrConsecutiveFailures);
        Console.Error.WriteLine($"{detail} Retrying in {delay.TotalSeconds:0.00} seconds...");
        try { await Task.Delay(delay, shutdown.Token); }
        catch (OperationCanceledException) { }
        continue;
    }

    if (vrConsecutiveFailures > 0)
    {
        vrLastRecoveredAt = DateTimeOffset.UtcNow;
        Console.WriteLine($"SteamVR compositor recovered after {vrConsecutiveFailures} failed attempt(s).");
    }
    vrConsecutiveFailures = 0;
    Console.WriteLine(detail);
    using (openVr)
    {
        try
        {
            profile = profileStore.Load();
            var settings = desktopSettingsReader.Load();
            ConfigureAll(openVr, profile);
            var lastConfigurationRead = Stopwatch.GetTimestamp();
            var lastTimingUpdate = 0L;
            var lastStrategyUpdate = 0L;
            var lastHealthReport = Stopwatch.GetTimestamp();
            var sessionFlags = EssentialWidgetStateFactory.CreateSessionFlags(
                LmuTelemetrySnapshot.Unavailable(LmuConnectionState.Disconnected, "Waiting."));
            var fuelStrategy = fuelTracker.Update(
                LmuTelemetrySnapshot.Unavailable(LmuConnectionState.Disconnected, "Waiting."),
                settings.FuelOptions());
            var raceControl = EssentialWidgetStateFactory.CreateRaceControl(
                LmuTelemetrySnapshot.Unavailable(LmuConnectionState.Disconnected, "Waiting."));

            while (!shutdown.IsCancellationRequested)
            {
                var now = Stopwatch.GetTimestamp();
                if (now - lastHealthReport >= Stopwatch.Frequency * 10)
                {
                    var health = telemetry.Health;
                    var presentationHealth = new PresentationHostHealth(
                        true,
                        vrRecoveryAttempts,
                        vrLastRecoveredAt,
                        string.Empty);
                    Console.WriteLine(
                        $"HEALTH read p99={health.P99ReadMilliseconds:0.000} ms " +
                        $"stale={health.StaleAgeMilliseconds:0} ms " +
                        $"vr-recovery={vrRecoveryAttempts} " +
                        $"last-recovered={vrLastRecoveredAt?.ToString("O") ?? "never"}");
                    if (!string.IsNullOrWhiteSpace(diagnosticsPath))
                    {
                        SteamVrDiagnosticsWriter.TryWrite(
                            diagnosticsPath,
                            health,
                            presentationHealth,
                            profile);
                    }
                    lastHealthReport = now;
                }
                if (now - lastConfigurationRead >= Stopwatch.Frequency)
                {
                    var nextProfile = profileStore.Load();
                    var nextSettings = desktopSettingsReader.Load();
                    if (nextProfile != profile)
                    {
                        profile = nextProfile;
                        ConfigureAll(openVr, profile);
                    }
                    settings = nextSettings;
                    lastConfigurationRead = now;
                }

                var snapshot = telemetry.Latest;
                officialOptimal.Update(snapshot);
                var ended = snapshot.Session?.GamePhase == LmuGamePhase.SessionOver;
                var live = ended
                    ? LmuTelemetrySnapshot.Unavailable(
                        LmuConnectionState.Disconnected,
                        "Session ended.")
                    : snapshot;
                var dashboard = EssentialWidgetStateFactory.CreateDashboard(live);
                var trackedSectors = sectors.Update(live, dashboard.SectorTimes);
                dashboard = dashboard with
                {
                    SectorTimes = trackedSectors,
                    BestLapTimeSeconds = sectors.PersonalBestLapTimeSeconds > 0
                        ? sectors.PersonalBestLapTimeSeconds
                        : dashboard.BestLapTimeSeconds,
                    OptimalLapTimeSeconds = officialOptimal.GetOptimal(snapshot),
                };
                var inputs = EssentialWidgetStateFactory.CreateInputs(live);
                UpdatePedalHistory(
                    pedalHistory,
                    ref pedalSession,
                    SessionIdentity(snapshot),
                    inputs,
                    settings);
                var samples = pedalHistory.ToArray();
                var style = VrRenderStyle.From(settings);
                Submit(openVr, dashboardKey, profile.Dashboard,
                    VrWidgetTextureRenderer.Dashboard(dashboard, style, samples));
                Submit(openVr, inputsKey, profile.Inputs,
                    VrWidgetTextureRenderer.Inputs(inputs, style, samples));
                Submit(openVr, priorityAlertKey, profile.PriorityAlert,
                    VrWidgetTextureRenderer.PriorityAlert(
                        dashboard,
                        sessionFlags,
                        fuelStrategy,
                        raceControl,
                        style,
                        settings));

                if (lastTimingUpdate == 0 || now - lastTimingUpdate >= Stopwatch.Frequency / 10)
                {
                    Submit(openVr, standingsKey, profile.LiveStandings,
                        VrWidgetTextureRenderer.LiveStandings(
                            EssentialWidgetStateFactory.CreateLiveStandings(
                                live,
                                settings.LiveStandingsMaximumRows),
                            style));
                    Submit(openVr, relativeKey, profile.Relative,
                        VrWidgetTextureRenderer.Relative(
                            EssentialWidgetStateFactory.CreateRelative(
                                live,
                                settings.RelativeCarsEachSide),
                            style));
                    raceControl = EssentialWidgetStateFactory.CreateRaceControl(live);
                    Submit(openVr, raceControlKey, profile.RaceControl,
                        VrWidgetTextureRenderer.RaceControl(raceControl, style));
                    lastTimingUpdate = now;
                }

                if (lastStrategyUpdate == 0 || now - lastStrategyUpdate >= Stopwatch.Frequency / 5)
                {
                    fuelStrategy = fuelTracker.Update(live, settings.FuelOptions());
                    Submit(openVr, fuelKey, profile.FuelStrategy,
                        VrWidgetTextureRenderer.Fuel(fuelStrategy, style));
                    sessionFlags = EssentialWidgetStateFactory.CreateSessionFlags(live);
                    Submit(openVr, sessionKey, profile.SessionFlags,
                        VrWidgetTextureRenderer.Session(sessionFlags, style));
                    lastStrategyUpdate = now;
                }

                var frameDelay = TimeSpan.FromSeconds(1d / settings.RefreshRateHz);
                await Task.Delay(frameDelay, shutdown.Token);
            }
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Console.Error.WriteLine($"SteamVR connection lost: {exception.Message}");
            Console.Error.WriteLine("The host will reconnect automatically.");
            vrConsecutiveFailures++;
            vrRecoveryAttempts++;
        }
    }
}

return 0;

static SteamVrProfile ApplyPreset(SteamVrProfile current, string[] arguments)
{
    var index = Array.FindIndex(arguments, value =>
        string.Equals(value, "--vr-preset", StringComparison.OrdinalIgnoreCase));
    if (index < 0 || index + 1 >= arguments.Length) return current;
    return arguments[index + 1].ToLowerInvariant() switch
    {
        "default" => SteamVrProfile.Default,
        "compact" => SteamVrProfile.Compact,
        "endurance" => SteamVrProfile.Endurance,
        _ => current,
    };
}

static string? ArgumentValue(string[] arguments, string name)
{
    var index = Array.FindIndex(arguments, value =>
        string.Equals(value, name, StringComparison.OrdinalIgnoreCase));
    return index >= 0 && index + 1 < arguments.Length
        ? arguments[index + 1]
        : null;
}

static SteamVrProfile Calibrate(SteamVrProfile current)
{
    Console.Write("VR panel scale (0.6 - 1.8, default 1.0): ");
    var scale = float.TryParse(
        Console.ReadLine(),
        System.Globalization.NumberStyles.Float,
        System.Globalization.CultureInfo.InvariantCulture,
        out var parsedScale) ? parsedScale : 1f;
    Console.Write("VR distance in meters (0.6 - 3.0, default 1.5): ");
    var distance = float.TryParse(
        Console.ReadLine(),
        System.Globalization.NumberStyles.Float,
        System.Globalization.CultureInfo.InvariantCulture,
        out var parsedDistance) ? parsedDistance : 1.5f;
    return current.Calibrate(scale, distance);
}

static void ConfigureAll(OpenVrNative openVr, SteamVrProfile profile)
{
    Configure(openVr, dashboardKey, "RedFox Dashboard", profile.Dashboard);
    Configure(openVr, inputsKey, "RedFox Driver Inputs", profile.Inputs);
    Configure(openVr, standingsKey, "RedFox Live Standings", profile.LiveStandings);
    Configure(openVr, relativeKey, "RedFox Relative", profile.Relative);
    Configure(openVr, fuelKey, "RedFox Fuel Strategy", profile.FuelStrategy);
    Configure(openVr, sessionKey, "RedFox Session", profile.SessionFlags);
    Configure(openVr, raceControlKey, "RedFox Race Control", profile.RaceControl);
    Configure(openVr, priorityAlertKey, "RedFox Priority Alert", profile.PriorityAlert);
}

static void Configure(
    OpenVrNative openVr,
    string key,
    string name,
    SteamVrWidgetPlacement placement)
{
    if (placement.Visible)
        openVr.CreateOrReplaceOverlay(key, name, placement.ToSettings());
    else
        openVr.Hide(key);
}

static void Submit(
    OpenVrNative openVr,
    string key,
    SteamVrWidgetPlacement placement,
    VrRenderedFrame frame)
{
    if (placement.Visible)
        openVr.SubmitRgba(key, frame.Pixels, frame.Width, frame.Height);
}

static void UpdatePedalHistory(
    Queue<VrPedalSample> history,
    ref string currentSession,
    string session,
    InputsWidgetState inputs,
    VrDesktopSettings settings)
{
    if (!string.Equals(currentSession, session, StringComparison.Ordinal))
    {
        currentSession = session;
        history.Clear();
    }
    if (!inputs.Available)
    {
        history.Clear();
        return;
    }
    history.Enqueue(new((float)inputs.Throttle, (float)inputs.Brake));
    var capacity = Math.Clamp(settings.RefreshRateHz * settings.PedalHistorySeconds, 180, 1_200);
    while (history.Count > capacity) history.Dequeue();
}

static string SessionIdentity(LmuTelemetrySnapshot snapshot) =>
    snapshot.Session is { } session && snapshot.Player is { } player
        ? $"{session.TrackName}\u001f{session.SessionCode}\u001f{player.VehicleId}"
        : string.Empty;
