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
    private void ExitApplication()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        CompositionTarget.Rendering -= OnRendering;
        _windowTimer.Stop();
        if (_telemetryRuntime is not null)
        {
            _telemetryRuntime.SnapshotPublished -= OnTelemetrySnapshot;
        }
        _telemetryRuntime?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _nativeAnalyticsTask?.Wait(TimeSpan.FromSeconds(1));
        _remoteDashboard?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _remoteDashboard = null;
        _nativeDashboard?.Dispose();
        _nativeDashboard = null;
        _nativeInputs?.Dispose();
        _nativeInputs = null;
        _nativeTiming?.Dispose();
        _nativeTiming = null;
        _officialTimingOptimal?.Dispose();
        _officialTimingOptimal = null;
        _configurationWindow?.Close();
        _toolbar?.Close();
        _overlay?.Close();
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        _trayIconImage?.Dispose();
        _trayIconImage = null;

        Shutdown();
    }
}
