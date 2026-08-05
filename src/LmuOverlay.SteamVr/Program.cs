using LmuOverlay.Core;
using LmuOverlay.LmuSharedMemory;
using LmuOverlay.SteamVr;
using LmuOverlay.Widgets;

const string dashboardKey = "com.redfoxracing.lmuoverlay.dashboard";
const string standingsKey = "com.redfoxracing.lmuoverlay.standings";
const string relativeKey = "com.redfoxracing.lmuoverlay.relative";
const string fuelKey = "com.redfoxracing.lmuoverlay.fuel";
const string sessionKey = "com.redfoxracing.lmuoverlay.session";

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("The SteamVR host requires Windows.");
    return 2;
}

var profileStore = new SteamVrProfileStore();
var profile = profileStore.Load();
var presetIndex = Array.FindIndex(args, value =>
    string.Equals(value, "--vr-preset", StringComparison.OrdinalIgnoreCase));
if (presetIndex >= 0 && presetIndex + 1 < args.Length)
{
    profile = args[presetIndex + 1].ToLowerInvariant() switch
    {
        "compact" => SteamVrProfile.Compact,
        "endurance" => SteamVrProfile.Endurance,
        _ => profile,
    };
    profileStore.Save(profile);
}

if (args.Contains("--calibrate", StringComparer.OrdinalIgnoreCase))
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
    profile = profile.Calibrate(scale, distance).Sanitize();
    profileStore.Save(profile);
    Console.WriteLine("SteamVR calibration saved.");
}

if (!OpenVrNative.TryConnect(out var openVr, out var detail) || openVr is null)
{
    Console.Error.WriteLine(detail);
    Console.Error.WriteLine("Start SteamVR and run LmuOverlay.SteamVr.exe again.");
    return 1;
}

Console.WriteLine(detail);
using (openVr)
{
    Configure(dashboardKey, "RedFox Dashboard", profile.Dashboard);
    Configure(standingsKey, "RedFox Live Standings", profile.LiveStandings);
    Configure(relativeKey, "RedFox Relative", profile.Relative);
    Configure(fuelKey, "RedFox Fuel Strategy", profile.FuelStrategy);
    Configure(sessionKey, "RedFox Session", profile.SessionFlags);

    await using var telemetry = new TelemetryRuntime(
        () => new LmuSharedMemoryReader(),
        TimeSpan.FromMilliseconds(4),
        TimeSpan.FromSeconds(1));
    telemetry.Start();
    var fuelTracker = new FuelStrategyTracker();

    using var shutdown = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        shutdown.Cancel();
    };

    Console.WriteLine("SteamVR overlay is active. Press Ctrl+C to close it.");
    try
    {
        var frameNumber = 0;
        while (!shutdown.IsCancellationRequested)
        {
            var snapshot = telemetry.Latest;
            Submit(
                dashboardKey,
                profile.Dashboard,
                VrWidgetTextureRenderer.Dashboard(
                    EssentialWidgetStateFactory.CreateDashboard(snapshot)));

            if (frameNumber % 30 == 0)
            {
                Submit(
                    standingsKey,
                    profile.LiveStandings,
                    VrWidgetTextureRenderer.LiveStandings(
                        EssentialWidgetStateFactory.CreateLiveStandings(snapshot)));
                Submit(
                    relativeKey,
                    profile.Relative,
                    VrWidgetTextureRenderer.Relative(
                        EssentialWidgetStateFactory.CreateRelative(snapshot)));
                Submit(
                    fuelKey,
                    profile.FuelStrategy,
                    VrWidgetTextureRenderer.Fuel(fuelTracker.Update(snapshot)));
                Submit(
                    sessionKey,
                    profile.SessionFlags,
                    VrWidgetTextureRenderer.Session(
                        EssentialWidgetStateFactory.CreateSessionFlags(snapshot)));
            }

            frameNumber++;
            await Task.Delay(16, shutdown.Token);
        }
    }
    catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
    {
    }

    void Configure(string key, string name, SteamVrWidgetPlacement placement)
    {
        if (placement.Visible)
        {
            openVr.CreateOrReplaceOverlay(key, name, placement.ToSettings());
        }
    }

    void Submit(string key, SteamVrWidgetPlacement placement, VrRenderedFrame frame)
    {
        if (placement.Visible)
        {
            openVr.SubmitRgba(key, frame.Pixels, frame.Width, frame.Height);
        }
    }
}

return 0;
