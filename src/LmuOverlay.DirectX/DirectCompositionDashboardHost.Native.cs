using System.Drawing;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using SharpGen.Runtime;
using LmuOverlay.Widgets;
using Vortice.Direct2D1;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DirectComposition;
using Vortice.DirectWrite;
using Vortice.DXGI;
using Vortice.Mathematics;
using static Vortice.Direct2D1.D2D1;
using static Vortice.Direct3D11.D3D11;
using static Vortice.DirectComposition.DComp;
using static Vortice.DirectWrite.DWrite;
using static Vortice.DXGI.DXGI;

namespace LmuOverlay.DirectX;

internal sealed partial class DirectCompositionDashboardHost
{
    private void ReleaseDrawingResources()
    {
        _white?.Dispose(); _white = null;
        _muted?.Dispose(); _muted = null;
        _green?.Dispose(); _green = null;
        _cyan?.Dispose(); _cyan = null;
        _amber?.Dispose(); _amber = null;
        _red?.Dispose(); _red = null;
        _blue?.Dispose(); _blue = null;
        _purple?.Dispose(); _purple = null;
        _panel?.Dispose(); _panel = null;
        _border?.Dispose(); _border = null;
        _drawing?.Dispose(); _drawing = null;
        _surface?.Dispose(); _surface = null;
    }

    public void Dispose()
    {
        ReleaseDrawingResources();
        foreach (var format in _textFormats.Values)
        {
            format.Dispose();
        }
        _writeFactory.Dispose();
        _compositionVisual.Dispose();
        _compositionTarget.Dispose();
        _compositionDevice.Dispose();
        _swapChain?.Dispose();
        _dxgiFactory.Dispose();
        _dxgiDevice.Dispose();
        _d3dContext.Dispose();
        _d3dDevice.Dispose();
        DestroyWindow(_window);
    }

    private static IntPtr CreateOverlayWindow()
    {
        var className = $"LmuOverlay.DirectX.{Environment.ProcessId}";
        var instance = GetModuleHandle(null);
        var windowClass = new WindowClass
        {
            Size = (uint)Marshal.SizeOf<WindowClass>(),
            Instance = instance,
            ClassName = className,
            WindowProcedure = Marshal.GetFunctionPointerForDelegate(WindowProc),
        };
        if (RegisterClassEx(ref windowClass) == 0)
        {
            throw new InvalidOperationException($"RegisterClassEx failed: {Marshal.GetLastWin32Error()}.");
        }

        var window = CreateWindowEx(
            WsExTopmost | WsExTransparent | WsExToolWindow | WsExNoActivate | WsExNoRedirectionBitmap,
            className,
            "LMU DirectX Dashboard",
            WsPopup,
            0,
            0,
            1,
            1,
            IntPtr.Zero,
            IntPtr.Zero,
            instance,
            IntPtr.Zero);
        if (window == IntPtr.Zero)
        {
            throw new InvalidOperationException($"CreateWindowEx failed: {Marshal.GetLastWin32Error()}.");
        }

        return window;
    }

    private static IntPtr ProcessWindowMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam) =>
        message switch
        {
            WmNcHitTest => new IntPtr(HtTransparent),
            WmEraseBackground => new IntPtr(1),
            WmDestroy => IntPtr.Zero,
            _ => DefWindowProc(window, message, wParam, lParam),
        };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint Size;
        public uint Style;
        public IntPtr WindowProcedure;
        public int ClassExtra;
        public int WindowExtra;
        public IntPtr Instance;
        public IntPtr Icon;
        public IntPtr Cursor;
        public IntPtr Background;
        public string? MenuName;
        public string? ClassName;
        public IntPtr SmallIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public IntPtr Window;
        public uint Message;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public int PointX;
        public int PointY;
        public uint Private;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr WindowProcedure(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassEx(ref WindowClass windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(uint extendedStyle, string className, string windowName, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern bool PeekMessage(out NativeMessage message, IntPtr window, uint filterMinimum, uint filterMaximum, uint removeMessage);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref NativeMessage message);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref NativeMessage message);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
