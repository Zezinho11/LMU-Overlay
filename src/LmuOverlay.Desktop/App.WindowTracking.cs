using System.Drawing;
using System.Diagnostics;
using System.Net.Sockets;
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
    private void OnWindowTick(object? sender, EventArgs e)
    {
        if (_overlay is null)
        {
            return;
        }
        EnsureRemoteDashboard(_overlay.CurrentProfile.Settings);

        var snapshot = _telemetryRuntime?.Latest
            ?? LmuTelemetrySnapshot.Unavailable(
                LmuConnectionState.Disconnected,
                "Waiting for LMU shared memory.");
        if (_telemetryRuntime is not null)
        {
            _overlay.UpdateRuntimeHealth(_telemetryRuntime.Health);
        }
        _overlay.UpdatePresentationHealth(new DesktopPresentationHealth(
            _nativeDashboard?.Health ?? new(false, 0, null, string.Empty),
            _nativeInputs?.Health ?? new(false, 0, null, string.Empty),
            _nativeTiming?.Health ?? new(false, 0, null, string.Empty)));
        var gameBounds = LmuWindowTracker.TryGetClientBounds();
        if (gameBounds is null && snapshot.State == LmuConnectionState.Connected)
        {
            var screen = WinForms.Screen.PrimaryScreen?.Bounds;
            if (screen is not null)
            {
                gameBounds = new System.Windows.Rect(
                    screen.Value.Left,
                    screen.Value.Top,
                    screen.Value.Width,
                    screen.Value.Height);
            }
        }

        if (gameBounds is null)
        {
            _gameBounds = null;
            Volatile.Write(ref _nativeDashboardConfiguration, null);
            Volatile.Write(ref _nativeInputsConfiguration, null);
            _nativeDashboard?.Hide(
                Interlocked.Increment(ref _nativeDashboardSequence));
            _nativeInputs?.Hide(
                Interlocked.Increment(ref _nativeInputsSequence));
            _nativeTiming?.Hide(
                Interlocked.Increment(ref _nativeTimingSequence));
            _overlay.SetGameAvailable(false);
            _toolbar?.SetGameAvailable(false);
            return;
        }

        _gameBounds = gameBounds;
        Volatile.Write(
            ref _nativeDashboardConfiguration,
            new NativeDashboardConfiguration(
                _overlay.GetNativeDashboardBounds(gameBounds.Value),
                _overlay.NativeDashboardShouldBeVisible,
                _overlay.NativeStyle with
                {
                    BackgroundOpacity = _overlay.NativeDashboardOpacity,
                }));
        Volatile.Write(
            ref _nativeInputsConfiguration,
            new NativeInputsConfiguration(
                _overlay.GetNativeInputsBounds(gameBounds.Value),
                _overlay.NativeInputsShouldBeVisible,
                _overlay.NativeStyle with
                {
                    BackgroundOpacity = _overlay.NativeInputsOpacity,
                }));
        PublishNativeTiming(snapshot, gameBounds.Value);
        _toolbar?.UpdateForGame(gameBounds.Value);
        var sinceRender = Stopwatch.GetTimestamp() - _lastRenderedAt;
        if (!_overlay.IsVisible ||
            sinceRender > Stopwatch.Frequency / 5)
        {
            RenderLatest(forceSlowUpdate: true);
        }
    }

    private void EnsureRemoteDashboard(
        LmuOverlay.Configuration.OverlayProfileSettings settings)
    {
        if (!settings.EnableRemoteDashboard)
        {
            if (_remoteDashboard is not null)
            {
                _remoteDashboard.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _remoteDashboard = null;
            }
            return;
        }
        if (_remoteDashboard is not null &&
            _remoteDashboard.Port == settings.RemoteDashboardPort &&
            string.Equals(_remoteDashboard.Token, settings.RemoteDashboardToken,
                StringComparison.Ordinal))
        {
            return;
        }
        _remoteDashboard?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _remoteDashboard = null;
        try
        {
            _remoteDashboard = new(
                settings.RemoteDashboardPort,
                settings.RemoteDashboardToken);
        }
        catch (SocketException)
        {
            // Keep the desktop overlay running when the chosen LAN port is in use.
        }
    }
}
