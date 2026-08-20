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
        _officialTimingOptimal?.Update(snapshot);
        var sessionEnded = snapshot.Session?.GamePhase == LmuGamePhase.SessionOver;
        var liveSnapshot = sessionEnded
            ? LmuTelemetrySnapshot.Unavailable(
                LmuConnectionState.Disconnected,
                "Session ended.")
            : snapshot;
        var dashboard = EssentialWidgetStateFactory.CreateDashboard(liveSnapshot);
        var trackedSectors = _nativeSectorReferenceTracker?.Update(
            liveSnapshot,
            dashboard.SectorTimes) ?? dashboard.SectorTimes;
        var liveOptimal = _officialTimingOptimal?.GetOptimal(snapshot) ?? 0;
        var persistedOptimal = _nativeSectorReferenceTracker?.ObserveOptimal(
            liveSnapshot,
            liveOptimal) ?? liveOptimal;
        dashboard = dashboard with
        {
            SectorTimes = trackedSectors,
            BestLapTimeSeconds =
                _nativeSectorReferenceTracker?.PersonalBestLapTimeSeconds > 0
                    ? _nativeSectorReferenceTracker.PersonalBestLapTimeSeconds
                    : dashboard.BestLapTimeSeconds,
            OptimalLapTimeSeconds = persistedOptimal,
        };
        var sessionKey = GetNativeSessionKey(snapshot);
        var capturedTimestamp = Stopwatch.GetTimestamp();
        if (sessionEnded)
        {
            Volatile.Write(ref _nativeFuelSaveFraction, 0);
            Interlocked.Exchange(ref _lastNativeFuelStrategyAt, 0);
        }
        else if (_overlay is { } overlay &&
                 (Volatile.Read(ref _lastNativeFuelStrategyAt) == 0 ||
                  capturedTimestamp - Volatile.Read(ref _lastNativeFuelStrategyAt) >=
                  Stopwatch.Frequency / 5))
        {
            var settings = overlay.CurrentProfile.Settings;
            var fuelSave = _nativeFuelStrategyTracker.Update(
                snapshot,
                new FuelStrategyOptions(
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
                    settings.ManualFuelCapacityLiters)).RequiredFuelSavingFraction;
            Volatile.Write(ref _nativeFuelSaveFraction, fuelSave);
            Interlocked.Exchange(ref _lastNativeFuelStrategyAt, capturedTimestamp);
        }
        var fuelSaveFraction = Volatile.Read(ref _nativeFuelSaveFraction);

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
                EssentialWidgetStateFactory.CreateInputs(liveSnapshot),
                inputsConfiguration.Bounds,
                inputsConfiguration.Visible && inputsRenderer.IsAvailable,
                Interlocked.Increment(ref _nativeInputsSequence),
                capturedTimestamp,
                sessionKey,
                inputsConfiguration.Style));
        }
    }

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
