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
    private void OnRendering(object? sender, EventArgs e)
    {
        if (_overlay is null || _gameBounds is null)
        {
            return;
        }

        var now = Stopwatch.GetTimestamp();
        var refreshRate = Math.Clamp(_overlay.RequestedRefreshRateHz, 30, 144);
        var minimumTicks = Stopwatch.Frequency / (double)refreshRate;
        if (_lastRenderedAt > 0 && now - _lastRenderedAt < minimumTicks)
        {
            return;
        }

        RenderLatest(forceSlowUpdate: false);
    }

    private void RenderLatest(bool forceSlowUpdate)
    {
        if (_overlay is null || _gameBounds is null)
        {
            return;
        }

        var now = Stopwatch.GetTimestamp();
        var slowUpdate = forceSlowUpdate ||
            _lastSlowUpdateAt == 0 ||
            now - _lastSlowUpdateAt >= Stopwatch.Frequency / 5;
        var snapshot = _telemetryRuntime?.Latest
            ?? LmuTelemetrySnapshot.Unavailable(
                LmuConnectionState.Disconnected,
                "Waiting for LMU shared memory.");
        if (ReferenceEquals(snapshot, _lastRenderedSnapshot) && !slowUpdate)
        {
            _lastRenderedAt = now;
            return;
        }

        _overlay.SetNativeDashboardActive(_nativeDashboard?.IsAvailable == true);
        _overlay.SetNativeInputsActive(_nativeInputs?.IsAvailable == true);
        _overlay.SetNativeTimingActive(_nativeTiming?.IsAvailable == true);
        _overlay.UpdateFrame(
            _gameBounds.Value,
            snapshot,
            slowUpdate,
            _officialTimingOptimal?.GetOptimal(snapshot) ?? 0,
            LatestDirectSteering());
        _lastRenderedSnapshot = snapshot;
        _lastRenderedAt = now;
        if (slowUpdate)
        {
            _lastSlowUpdateAt = now;
        }
    }
}
