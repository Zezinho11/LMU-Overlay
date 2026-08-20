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
    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _trayIconImage?.Dispose();
        base.OnExit(e);
    }
}
