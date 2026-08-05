using System.Drawing;
using System.Windows.Threading;
using LmuOverlay.Core;
using LmuOverlay.Domain;
using LmuOverlay.LmuSharedMemory;
using WinForms = System.Windows.Forms;

namespace LmuOverlay.Desktop;

public partial class App
{
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Render)
    {
        Interval = TimeSpan.FromMilliseconds(1000d / 30),
    };

    private OverlayWindow? _overlay;
    private OverlayToolbarWindow? _toolbar;
    private ConfigurationWindow? _configurationWindow;
    private WinForms.NotifyIcon? _trayIcon;
    private Icon? _trayIconImage;
    private TelemetryRuntime? _telemetryRuntime;
    private bool _isExiting;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
            CrashLogWriter.TryWrite(args.Exception);
        _overlay = new OverlayWindow(new LayoutStore());
        _toolbar = new OverlayToolbarWindow(_overlay, ShowConfiguration);
        _telemetryRuntime = new TelemetryRuntime(
            () => new LmuSharedMemoryReader(),
            TimeSpan.FromMilliseconds(16),
            TimeSpan.FromSeconds(1));
        _telemetryRuntime.Start();
        CreateTrayIcon();
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void CreateTrayIcon()
    {
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Configurar widgets", null, (_, _) => ShowConfiguration());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Editar layout", null, (_, _) => SetEditMode(true));
        menu.Items.Add("Bloquear overlay", null, (_, _) => SetEditMode(false));
        menu.Items.Add("Restaurar layout", null, (_, _) => _overlay?.ResetLayout());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => ExitApplication());

        _trayIconImage = Environment.ProcessPath is { Length: > 0 } executablePath
            ? Icon.ExtractAssociatedIcon(executablePath)
            : null;
        _trayIcon = new WinForms.NotifyIcon
        {
            Icon = _trayIconImage ?? SystemIcons.Application,
            Text = "LMU Overlay",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _trayIcon.DoubleClick += (_, _) => SetEditMode(!(_overlay?.IsEditMode ?? false));
    }

    private void SetEditMode(bool enabled)
    {
        _overlay?.SetEditMode(enabled);
        _toolbar?.SyncFromOverlay();
    }

    private void ShowConfiguration()
    {
        if (_overlay is null)
        {
            return;
        }

        if (_configurationWindow is null)
        {
            _configurationWindow = new ConfigurationWindow(_overlay);
            _configurationWindow.Closed += (_, _) => _configurationWindow = null;
            _configurationWindow.Show();
        }
        else
        {
            _configurationWindow.Activate();
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_overlay is null)
        {
            return;
        }

        var snapshot = _telemetryRuntime?.Latest
            ?? LmuTelemetrySnapshot.Unavailable(
                LmuConnectionState.Disconnected,
                "Waiting for LMU shared memory.");
        if (_telemetryRuntime is not null)
        {
            _overlay.UpdateRuntimeHealth(_telemetryRuntime.Health);
        }
        var requestedInterval = TimeSpan.FromMilliseconds(
            1000d / Math.Clamp(_overlay.RequestedRefreshRateHz, 10, 60));
        if (Math.Abs((_timer.Interval - requestedInterval).TotalMilliseconds) >= 1)
        {
            _timer.Interval = requestedInterval;
        }

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
            _overlay.SetGameAvailable(false);
            _toolbar?.SetGameAvailable(false);
            return;
        }

        _overlay.UpdateFrame(gameBounds.Value, snapshot);
        _toolbar?.UpdateForGame(gameBounds.Value);
    }

    private void ExitApplication()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        _timer.Stop();
        _telemetryRuntime?.DisposeAsync().AsTask().GetAwaiter().GetResult();
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

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _trayIconImage?.Dispose();
        base.OnExit(e);
    }
}
