using System.Drawing;
using System.Windows.Threading;
using LmuOverlay.Domain;
using LmuOverlay.LmuSharedMemory;
using WinForms = System.Windows.Forms;

namespace LmuOverlay.Desktop;

public partial class App
{
    private readonly DispatcherTimer _timer = new()
    {
        Interval = TimeSpan.FromMilliseconds(100),
    };

    private OverlayWindow? _overlay;
    private WinForms.NotifyIcon? _trayIcon;
    private LmuSharedMemoryReader? _reader;
    private DateTimeOffset _lastReconnectAttempt;
    private bool _isExiting;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        _overlay = new OverlayWindow(new LayoutStore());
        CreateTrayIcon();
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void CreateTrayIcon()
    {
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Editar layout", null, (_, _) => SetEditMode(true));
        menu.Items.Add("Bloquear overlay", null, (_, _) => SetEditMode(false));
        menu.Items.Add("Restaurar layout", null, (_, _) => _overlay?.ResetLayout());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => ExitApplication());

        _trayIcon = new WinForms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "LMU Overlay",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _trayIcon.DoubleClick += (_, _) => SetEditMode(!(_overlay?.IsEditMode ?? false));
    }

    private void SetEditMode(bool enabled)
    {
        _overlay?.SetEditMode(enabled);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_overlay is null)
        {
            return;
        }

        EnsureReader();
        var snapshot = _reader?.ReadTelemetrySnapshot()
            ?? LmuTelemetrySnapshot.Unavailable(
                LmuConnectionState.Disconnected,
                "Waiting for LMU shared memory.");

        if (snapshot.State == LmuConnectionState.Disconnected)
        {
            _reader?.Dispose();
            _reader = null;
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
            return;
        }

        _overlay.UpdateFrame(gameBounds.Value, snapshot);
    }

    private void EnsureReader()
    {
        if (_reader is not null ||
            DateTimeOffset.UtcNow - _lastReconnectAttempt < TimeSpan.FromSeconds(1))
        {
            return;
        }

        _lastReconnectAttempt = DateTimeOffset.UtcNow;
        _reader = new LmuSharedMemoryReader();
    }

    private void ExitApplication()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        _timer.Stop();
        _reader?.Dispose();
        _overlay?.Close();
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }

        Shutdown();
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _reader?.Dispose();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
