using System.Drawing;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Threading;
using LmuOverlay.Core;
using LmuOverlay.Domain;
using LmuOverlay.DirectX;
using LmuOverlay.LmuSharedMemory;
using LmuOverlay.Widgets;
using WinForms = System.Windows.Forms;

namespace LmuOverlay.Desktop;

public partial class App
{
    private void OnTelemetrySnapshot(LmuTelemetrySnapshot snapshot)
    {
        var sessionEnded = snapshot.Session?.GamePhase == LmuGamePhase.SessionOver;
        var liveSnapshot = sessionEnded
            ? LmuTelemetrySnapshot.Unavailable(
                LmuConnectionState.Disconnected,
                "Session ended.")
            : snapshot;
        var dashboard = EssentialWidgetStateFactory.CreateDashboard(liveSnapshot) with
        {
            EngineRpmFraction = _nativeShiftLightTiming.Update(liveSnapshot.Player),
        };
        var profileSettings = _overlay?.CurrentProfile.Settings ?? new();
        var directSteering = ReadDirectSteering(profileSettings);
        var sessionKey = GetNativeSessionKey(snapshot);
        var analytics = Volatile.Read(ref _nativeDashboardAnalytics);
        if (analytics is not null &&
            string.Equals(analytics.SessionKey, sessionKey, StringComparison.Ordinal))
        {
            dashboard = dashboard with
            {
                SectorTimes = analytics.SectorTimes,
                BestLapTimeSeconds = analytics.BestLapTimeSeconds > 0
                    ? analytics.BestLapTimeSeconds
                    : dashboard.BestLapTimeSeconds,
                OptimalLapTimeSeconds = analytics.OptimalLapTimeSeconds,
            };
        }
        _remoteDashboard?.Publish(dashboard);
        var capturedTimestamp = Stopwatch.GetTimestamp();
        var fuelSaveFraction = sessionEnded ? 0 : analytics?.FuelSaveFraction ?? 0;

        var dashboardRenderer = _nativeDashboard;
        var dashboardConfiguration = Volatile.Read(ref _nativeDashboardConfiguration);
        if (dashboardRenderer is not null && dashboardConfiguration is not null)
        {
            dashboardRenderer.Publish(new NativeDashboardFrame(
                dashboard,
                dashboardConfiguration.Bounds,
                dashboardConfiguration.Visible && dashboardRenderer.IsAvailable,
                Interlocked.Increment(ref _nativeDashboardSequence),
                capturedTimestamp,
                fuelSaveFraction,
                sessionKey,
                dashboardConfiguration.Style));
        }

        var inputsRenderer = _nativeInputs;
        var inputsConfiguration = Volatile.Read(ref _nativeInputsConfiguration);
        if (inputsRenderer is not null && inputsConfiguration is not null)
        {
            inputsRenderer.Publish(new NativeInputsFrame(
                EssentialWidgetStateFactory.CreateInputs(
                    liveSnapshot,
                    profileSettings.SteeringWheelRangeDegrees,
                    directSteering),
                inputsConfiguration.Bounds,
                inputsConfiguration.Visible && inputsRenderer.IsAvailable,
                Interlocked.Increment(ref _nativeInputsSequence),
                capturedTimestamp,
                sessionKey,
                inputsConfiguration.Style));
        }

        // Keep persistence, strategy and HTTP target bookkeeping off the
        // capture thread. RPM, gear, shift lights and pedals are published
        // first and can never wait behind disk or derived-widget work.
        if (_lastNativeAnalyticsQueuedAt == 0 ||
            capturedTimestamp - _lastNativeAnalyticsQueuedAt >= Stopwatch.Frequency / 10)
        {
            _lastNativeAnalyticsQueuedAt = capturedTimestamp;
            QueueNativeDashboardAnalytics(new(
                snapshot,
                liveSnapshot,
                sessionKey,
                profileSettings,
                sessionEnded));
        }
    }

    private double? ReadDirectSteering(
        LmuOverlay.Configuration.OverlayProfileSettings settings)
    {
        if (!settings.UseDirectSteeringInput)
        {
            Volatile.Write(ref _directSteeringAvailable, 0);
            return null;
        }
        if (_steeringInputReader is null ||
            _steeringInputDeviceId != settings.SteeringInputDeviceId)
        {
            _steeringInputDeviceId = settings.SteeringInputDeviceId;
            _steeringInputReader = new(settings.SteeringInputDeviceId);
        }
        var sample = _steeringInputReader.Read();
        if (!sample.Available)
        {
            Volatile.Write(ref _directSteeringAvailable, 0);
            return null;
        }
        Interlocked.Exchange(
            ref _directSteeringBits,
            BitConverter.DoubleToInt64Bits(sample.NormalizedPosition));
        Volatile.Write(ref _directSteeringAvailable, 1);
        return sample.NormalizedPosition;
    }

    private double? LatestDirectSteering() =>
        Volatile.Read(ref _directSteeringAvailable) == 1
            ? BitConverter.Int64BitsToDouble(Interlocked.Read(ref _directSteeringBits))
            : null;

    private void QueueNativeDashboardAnalytics(NativeDashboardAnalyticsRequest request)
    {
        Volatile.Write(ref _nativeAnalyticsPending, request);
        if (Interlocked.CompareExchange(ref _nativeAnalyticsWorkerActive, 1, 0) == 0)
        {
            _nativeAnalyticsTask = Task.Run(ProcessNativeDashboardAnalytics);
        }
    }

    private void ProcessNativeDashboardAnalytics()
    {
        try
        {
            while (!_isExiting &&
                   Interlocked.Exchange(ref _nativeAnalyticsPending, null) is { } request)
            {
                try
                {
                    _officialTimingOptimal?.Update(request.Snapshot);
                    var raw = EssentialWidgetStateFactory.CreateDashboard(request.LiveSnapshot);
                    var sectors = _nativeSectorReferenceTracker?.Update(
                        request.LiveSnapshot,
                        raw.SectorTimes) ?? raw.SectorTimes;
                    var liveOptimal = _officialTimingOptimal?.GetOptimal(request.Snapshot) ?? 0;
                    var optimal = _nativeSectorReferenceTracker?.ObserveOptimal(
                        request.LiveSnapshot,
                        liveOptimal) ?? liveOptimal;
                    var fuelSave = request.SessionEnded
                        ? 0
                        : _nativeFuelStrategyTracker.Update(
                            request.Snapshot,
                            FuelOptions(request.Settings)).RequiredFuelSavingFraction;
                    Volatile.Write(ref _nativeDashboardAnalytics, new(
                        request.SessionKey,
                        sectors,
                        _nativeSectorReferenceTracker?.PersonalBestLapTimeSeconds ?? 0,
                        optimal,
                        fuelSave));
                }
                catch
                {
                    // Derived widgets are best-effort. A persistence or HTTP
                    // failure must not fault shutdown or the fast telemetry lane.
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref _nativeAnalyticsWorkerActive, 0);
            if (!_isExiting && Volatile.Read(ref _nativeAnalyticsPending) is { } pending)
            {
                QueueNativeDashboardAnalytics(pending);
            }
        }
    }

    private static FuelStrategyOptions FuelOptions(
        LmuOverlay.Configuration.OverlayProfileSettings settings) => new(
        settings.FuelReserveLaps,
        settings.EnergyReservePercent / 100,
        settings.ManualRemainingLaps,
        settings.MaximumStintLaps,
        settings.EstimatedPitLossSeconds,
        settings.AvailableTireSets,
        settings.TireWearLimitPercent / 100,
        settings.EstimatedTireChangeSeconds,
        settings.ManualRemainingMinutes,
        settings.ManualLapTimeSeconds,
        settings.ManualFuelPerLapLiters,
        settings.ManualFuelCapacityLiters);

    private string GetNativeSessionKey(LmuTelemetrySnapshot snapshot)
    {
        if (snapshot.Session is not { } session || snapshot.Player is not { } player)
        {
            return string.Empty;
        }

        if (_nativeSessionCode == session.SessionCode &&
            _nativeSessionVehicleId == player.VehicleId &&
            string.Equals(_nativeSessionTrack, session.TrackName, StringComparison.Ordinal))
        {
            return _nativeSessionKey;
        }

        _nativeSessionTrack = session.TrackName;
        _nativeSessionCode = session.SessionCode;
        _nativeSessionVehicleId = player.VehicleId;
        _nativeSessionKey =
            $"{session.TrackName}\u001f{session.SessionCode}\u001f{player.VehicleId}";
        return _nativeSessionKey;
    }

    private void PublishNativeTiming(
        LmuTelemetrySnapshot snapshot,
        System.Windows.Rect gameBounds)
    {
        var renderer = _nativeTiming;
        var overlay = _overlay;
        if (renderer is null || overlay is null)
        {
            return;
        }

        var configuration = new NativeTimingConfiguration(
            overlay.GetNativeLiveStandingsBounds(gameBounds),
            overlay.NativeLiveStandingsShouldBeVisible && renderer.IsAvailable,
            overlay.NativeLiveStandingsOpacity,
            overlay.GetNativeRelativeBounds(gameBounds),
            overlay.NativeRelativeShouldBeVisible && renderer.IsAvailable,
            overlay.NativeRelativeOpacity,
            overlay.NativeStyle,
            overlay.LiveStandingsMaximumRows,
            overlay.RelativeCarsEachSide);
        var timing = _nativeTimingTracker.Update(
            snapshot,
            configuration.LiveStandingsMaximumRows,
            configuration.RelativeCarsEachSide);
        _nativeLiveStandingsState = timing.Standings;
        _nativeRelativeState = timing.Relative;

        renderer.Publish(new NativeTimingFrame(
            _nativeLiveStandingsState,
            configuration.LiveStandingsBounds,
            configuration.LiveStandingsVisible,
            configuration.LiveStandingsOpacity,
            _nativeRelativeState,
            configuration.RelativeBounds,
            configuration.RelativeVisible,
            configuration.RelativeOpacity,
            Interlocked.Increment(ref _nativeTimingSequence),
            configuration.Style));
    }

    private sealed record NativeDashboardConfiguration(
        NativeDashboardBounds Bounds,
        bool Visible,
        NativeOverlayStyle Style);

    private sealed record NativeInputsConfiguration(
        NativeDashboardBounds Bounds,
        bool Visible,
        NativeOverlayStyle Style);

    private sealed record NativeDashboardAnalyticsRequest(
        LmuTelemetrySnapshot Snapshot,
        LmuTelemetrySnapshot LiveSnapshot,
        string SessionKey,
        LmuOverlay.Configuration.OverlayProfileSettings Settings,
        bool SessionEnded);

    private sealed record NativeDashboardAnalytics(
        string SessionKey,
        DashboardSectorTimes SectorTimes,
        double BestLapTimeSeconds,
        double OptimalLapTimeSeconds,
        double FuelSaveFraction);

    private sealed record NativeTimingConfiguration(
        NativeDashboardBounds LiveStandingsBounds,
        bool LiveStandingsVisible,
        double LiveStandingsOpacity,
        NativeDashboardBounds RelativeBounds,
        bool RelativeVisible,
        double RelativeOpacity,
        NativeOverlayStyle Style,
        int LiveStandingsMaximumRows,
        int RelativeCarsEachSide);
}
