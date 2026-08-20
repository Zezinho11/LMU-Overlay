using System.Diagnostics;
using LmuOverlay.Application;
using LmuOverlay.Core;
using LmuOverlay.Domain;
using LmuOverlay.LmuSharedMemory;
using LmuOverlay.Widgets;

namespace LmuOverlay.SteamVr;

public static partial class SteamVrHost
{
    private static SteamVrProfile ApplyPreset(SteamVrProfile current, string[] arguments)
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

    private static string? ArgumentValue(string[] arguments, string name)
    {
        var index = Array.FindIndex(arguments, value =>
            string.Equals(value, name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < arguments.Length
            ? arguments[index + 1]
            : null;
    }

    private static SteamVrProfile Calibrate(SteamVrProfile current)
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

    private static void ConfigureAll(OpenVrNative openVr, SteamVrProfile profile)
    {
        Configure(openVr, DashboardKey, "RedFox Dashboard", profile.Dashboard);
        Configure(openVr, InputsKey, "RedFox Driver Inputs", profile.Inputs);
        Configure(openVr, StandingsKey, "RedFox Live Standings", profile.LiveStandings);
        Configure(openVr, RelativeKey, "RedFox Relative", profile.Relative);
        Configure(openVr, FuelKey, "RedFox Fuel Strategy", profile.FuelStrategy);
        Configure(openVr, SessionKey, "RedFox Session", profile.SessionFlags);
        Configure(openVr, RaceControlKey, "RedFox Race Control", profile.RaceControl);
        Configure(openVr, PriorityAlertKey, "RedFox Priority Alert", profile.PriorityAlert);
    }

    private static void Configure(
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

    private static void Submit(
        OpenVrNative openVr,
        string key,
        SteamVrWidgetPlacement placement,
        VrRenderedFrame frame)
    {
        if (placement.Visible)
            openVr.SubmitRgba(key, frame.Pixels, frame.Width, frame.Height);
    }

    private static void UpdatePedalHistory(
        Queue<VrPedalSample> history,
        ref string currentSession,
        string session,
        InputsWidgetState inputs,
        OverlayProfileSettings settings)
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
        history.Enqueue(new(
            (float)inputs.Throttle,
            (float)inputs.Brake,
            inputs.AbsActive,
            inputs.TractionControlActive));
        var capacity = Math.Clamp(settings.RefreshRateHz * settings.PedalHistorySeconds, 180, 1_200);
        while (history.Count > capacity) history.Dequeue();
    }

    private static string SessionIdentity(LmuTelemetrySnapshot snapshot) =>
        snapshot.Session is { } session && snapshot.Player is { } player
            ? $"{session.TrackName}\u001f{session.SessionCode}\u001f{player.VehicleId}"
            : string.Empty;
}
