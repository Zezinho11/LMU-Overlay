using System.Diagnostics;
using LmuOverlay.Application;
using LmuOverlay.Core;
using LmuOverlay.Domain;
using LmuOverlay.LmuSharedMemory;
using LmuOverlay.SteamVr;
using LmuOverlay.Widgets;

namespace LmuOverlay.SteamVr;

public static partial class SteamVrHost
{
    private const string DashboardKey = "com.redfoxracing.lmuoverlay.dashboard";
    private const string InputsKey = "com.redfoxracing.lmuoverlay.inputs";
    private const string StandingsKey = "com.redfoxracing.lmuoverlay.standings";
    private const string RelativeKey = "com.redfoxracing.lmuoverlay.relative";
    private const string FuelKey = "com.redfoxracing.lmuoverlay.fuel";
    private const string SessionKey = "com.redfoxracing.lmuoverlay.session";
    private const string RaceControlKey = "com.redfoxracing.lmuoverlay.racecontrol";
    private const string PriorityAlertKey = "com.redfoxracing.lmuoverlay.priorityalert";

    public static async Task<int> RunAsync(string[] args)
    {
        const string dashboardKey = DashboardKey;
        const string inputsKey = InputsKey;
        const string standingsKey = StandingsKey;
        const string relativeKey = RelativeKey;
        const string fuelKey = FuelKey;
        const string sessionKey = SessionKey;
        const string raceControlKey = RaceControlKey;
        const string priorityAlertKey = PriorityAlertKey;

        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("The SteamVR host requires Windows.");
            return 2;
        }

        var profileStore = new SteamVrProfileStore();
        var desktopSettingsReader = new DesktopProfileSettingsReader();
        var frameComposer = new EssentialOverlayFrameComposer();
        var diagnosticsPath = ArgumentValue(args, "--diagnostics");
        var visualBaselinePath = ArgumentValue(args, "--capture-vr-baselines");
        if (!string.IsNullOrWhiteSpace(visualBaselinePath))
        {
            var files = VrVisualBaselineWriter.Write(
                visualBaselinePath,
                desktopSettingsReader.Load());
            Console.WriteLine($"SteamVR visual baselines written to {Path.GetFullPath(visualBaselinePath)}");
            foreach (var file in files) Console.WriteLine(file);
            return 0;
        }
        if (args.Contains("--configure", StringComparer.OrdinalIgnoreCase) ||
            args.Contains("--configure-vr", StringComparer.OrdinalIgnoreCase))
        {
            ApplicationConfiguration.Initialize();
        System.Windows.Forms.Application.Run(new SteamVrConfigurationForm(profileStore));
            return 0;
        }
        var startupSettings = desktopSettingsReader.Load();
        var compatibility = GameCompatibilityProbe.Detect();
        var vrPreflight = VrRuntimeProbe.Detect();
        var backend = VrBackendSelector.Select(
            ArgumentValue(args, "--vr-backend"),
            !args.Contains("--no-vr-fallback", StringComparer.OrdinalIgnoreCase),
            vrPreflight);
        Console.WriteLine($"LMU compatibility: {compatibility.State} · build {compatibility.InstalledBuildId} · {compatibility.Detail}");
        Console.WriteLine($"VR preflight: {vrPreflight.Detail}");
        Console.WriteLine($"VR backend: requested={backend.Requested} selected={backend.Selected} · {backend.Detail}");
        if (!backend.CanStart)
        {
            Console.Error.WriteLine("VR preflight failed safely. Desktop remains available.");
            return 3;
        }
        if (args.Contains("--safe-mode", StringComparer.OrdinalIgnoreCase) ||
            (!startupSettings.EnableSteamVr &&
             !args.Contains("--force-vr", StringComparer.OrdinalIgnoreCase)))
        {
            Console.WriteLine("SteamVR is disabled by safe mode or the active desktop profile.");
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
        using var officialOptimal = new OfficialTimingOptimalProvider(new()
        {
            Enabled = startupSettings.EnableOfficialTimingHttp &&
                !args.Contains("--disable-optimal-http", StringComparer.OrdinalIgnoreCase),
        });
        var sectors = new PersistentSectorReferenceTracker(
            new SectorReferenceStore(compatibilityGeneration: compatibility.CompatibilityGeneration),
            new PersonalBestLapStore(compatibilityGeneration: compatibility.CompatibilityGeneration));
        var fuelTracker = new FuelStrategyTracker();
        var timingTracker = new TimingWidgetTracker();
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
                        var essentialFrame = frameComposer.Compose(live);
                        var dashboard = essentialFrame.Dashboard;
                        var trackedSectors = sectors.Update(live, dashboard.SectorTimes);
                        var liveOptimal = officialOptimal.GetOptimal(snapshot);
                        var persistedOptimal = sectors.ObserveOptimal(live, liveOptimal);
                        dashboard = dashboard with
                        {
                            SectorTimes = trackedSectors,
                            BestLapTimeSeconds = sectors.PersonalBestLapTimeSeconds > 0
                                ? sectors.PersonalBestLapTimeSeconds
                                : dashboard.BestLapTimeSeconds,
                            OptimalLapTimeSeconds = persistedOptimal,
                        };
                        var inputs = essentialFrame.Inputs;
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
                            var timing = timingTracker.Update(
                                live,
                                settings.LiveStandingsMaximumRows,
                                settings.RelativeCarsEachSide);
                            Submit(openVr, standingsKey, profile.LiveStandings,
                                VrWidgetTextureRenderer.LiveStandings(
                                    timing.Standings,
                                    style));
                            Submit(openVr, relativeKey, profile.Relative,
                                VrWidgetTextureRenderer.Relative(
                                    timing.Relative,
                                    style));
                            raceControl = essentialFrame.RaceControl;
                            Submit(openVr, raceControlKey, profile.RaceControl,
                                VrWidgetTextureRenderer.RaceControl(raceControl, style));
                            lastTimingUpdate = now;
                        }

                        if (lastStrategyUpdate == 0 || now - lastStrategyUpdate >= Stopwatch.Frequency / 5)
                        {
                            fuelStrategy = fuelTracker.Update(live, settings.FuelOptions());
                            Submit(openVr, fuelKey, profile.FuelStrategy,
                                VrWidgetTextureRenderer.Fuel(fuelStrategy, style));
                            sessionFlags = essentialFrame.SessionFlags;
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

    }
}
