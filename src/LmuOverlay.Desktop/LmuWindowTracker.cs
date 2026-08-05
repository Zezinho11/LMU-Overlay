using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace LmuOverlay.Desktop;

internal static class LmuWindowTracker
{
    private delegate bool EnumWindowsProc(nint window, nint parameter);

    public static Rect? TryGetClientBounds()
    {
        nint match = 0;
        EnumWindows((window, _) =>
        {
            if (!IsWindowVisible(window) || GetWindowTextLength(window) == 0)
            {
                return true;
            }

            var title = new StringBuilder(256);
            _ = GetWindowText(window, title, title.Capacity);
            if (!title.ToString().Contains("Le Mans Ultimate", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            match = window;
            return false;
        }, 0);

        if (match == 0 || IsIconic(match) || !GetClientRect(match, out var client))
        {
            return null;
        }

        var origin = new Point();
        if (!ClientToScreen(match, ref origin))
        {
            return null;
        }

        var width = client.Right - client.Left;
        var height = client.Bottom - client.Top;
        var dpi = GetDpiForWindow(match);
        var pixelsToDip = dpi > 0 ? 96d / dpi : 1d;
        return width > 0 && height > 0
            ? new Rect(
                origin.X * pixelsToDip,
                origin.Y * pixelsToDip,
                width * pixelsToDip,
                height * pixelsToDip)
            : null;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, nint parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint window);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(nint window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint window, StringBuilder text, int maximumCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(nint window, out NativeRect rectangle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(nint window, ref Point point);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint window);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }
}
