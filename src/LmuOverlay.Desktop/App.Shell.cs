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
    private void StartVisualBaselineCapture(string? outputDirectory)
    {
        var destination = string.IsNullOrWhiteSpace(outputDirectory)
            ? System.IO.Path.Combine(AppContext.BaseDirectory, "visual-baselines")
            : System.IO.Path.GetFullPath(outputDirectory);
        var temporaryProfile = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "lmu-overlay-visual-qa",
            $"{Guid.NewGuid():N}.json");
        _overlay = new OverlayWindow(
            new LayoutStore(temporaryProfile),
            new SectorReferenceStore(temporaryProfile + ".sectors", "visual-fixture"),
            new PersonalBestLapStore(temporaryProfile + ".personal-bests", "visual-fixture"))
        {
            ShowActivated = false,
        };
        _overlay.Loaded += (_, _) => Dispatcher.BeginInvoke(() =>
        {
            _overlay.SetEditMode(true);
            var unavailable = LmuTelemetrySnapshot.Unavailable(
                LmuConnectionState.Disconnected,
                "Visual baseline");
            foreach (var (name, width, height) in new[]
            {
                ("720p", 1280d, 720d),
                ("1080p", 1920d, 1080d),
                ("ultrawide", 3440d, 1440d),
                ("1440p", 2560d, 1440d),
                ("4k", 3840d, 2160d),
            })
            {
                _overlay.UpdateFrame(new System.Windows.Rect(0, 0, width, height), unavailable);
                _overlay.CapturePng(
                    System.IO.Path.Combine(destination, $"overlay-{name}.png"),
                    (int)width,
                    (int)height);
            }

            _overlay.Close();
            try
            {
                System.IO.File.Delete(temporaryProfile);
            }
            catch (System.IO.IOException)
            {
            }

            Shutdown();
        });
        _overlay.Show();
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
}
