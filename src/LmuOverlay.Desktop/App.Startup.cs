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
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
            CrashLogWriter.TryWrite(args.Exception);
        if (e.Args.FirstOrDefault() == "--capture-visual-baselines")
        {
            StartVisualBaselineCapture(e.Args.Skip(1).FirstOrDefault());
            return;
        }

        _compatibility = GameCompatibilityProbe.Detect();
        _sectorReferenceStore = new(compatibilityGeneration: _compatibility.CompatibilityGeneration);
        _personalBestLapStore = new(compatibilityGeneration: _compatibility.CompatibilityGeneration);

        _nativeSectorReferenceTracker = new(
            _sectorReferenceStore,
            _personalBestLapStore);
        _overlay = new OverlayWindow(
            new LayoutStore(),
            _sectorReferenceStore,
            _personalBestLapStore);
        _safeMode = e.Args.Contains("--safe-mode", StringComparer.OrdinalIgnoreCase);
        var settings = _overlay.CurrentProfile.Settings;
        var nativeEnabled = !_safeMode &&
            settings.EnableNativeRendering &&
            !e.Args.Contains("--disable-native-rendering", StringComparer.OrdinalIgnoreCase);
        var officialTimingEnabled = !_safeMode &&
            settings.EnableOfficialTimingHttp &&
            !e.Args.Contains("--disable-optimal-http", StringComparer.OrdinalIgnoreCase);
        if (nativeEnabled)
        {
            _nativeDashboard = new NativeDashboardRenderer();
            _nativeInputs = new NativeInputsRenderer();
            _nativeTiming = new NativeTimingRenderer();
        }
        _officialTimingOptimal = new OfficialTimingOptimalProvider(new()
        {
            Enabled = officialTimingEnabled,
        });
        _toolbar = new OverlayToolbarWindow(_overlay, ShowConfiguration);
        _telemetryRuntime = new TelemetryRuntime(
            () => new LmuSharedMemoryReader(),
            // LMU's named event wakes capture immediately. Eight milliseconds is
            // only the safety fallback when an event is missed or unavailable.
            TimeSpan.FromMilliseconds(8),
            TimeSpan.FromSeconds(1));
        _telemetryRuntime.SnapshotPublished += OnTelemetrySnapshot;
        _telemetryRuntime.Start();
        CreateTrayIcon();
        _windowTimer.Tick += OnWindowTick;
        _windowTimer.Start();
        CompositionTarget.Rendering += OnRendering;
    }
}
