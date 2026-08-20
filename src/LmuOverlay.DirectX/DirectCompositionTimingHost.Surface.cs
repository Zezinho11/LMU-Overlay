using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using LmuOverlay.Widgets;
using SharpGen.Runtime;
using Vortice.Direct2D1;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DirectComposition;
using Vortice.DirectWrite;
using Vortice.DXGI;
using Vortice.Mathematics;
using static Vortice.Direct3D11.D3D11;
using static Vortice.DirectComposition.DComp;
using static Vortice.DirectWrite.DWrite;
using static Vortice.DXGI.DXGI;

namespace LmuOverlay.DirectX;

internal sealed partial class DirectCompositionTimingHost
{
    private sealed class TimingSurface : IDisposable
    {
        private const uint WsPopup = 0x80000000;
        private const uint WsExTopmost = 0x00000008;
        private const uint WsExTransparent = 0x00000020;
        private const uint WsExToolWindow = 0x00000080;
        private const uint WsExNoActivate = 0x08000000;
        private const uint WsExNoRedirectionBitmap = 0x00200000;
        private const uint SwpNoActivate = 0x0010;
        private const uint SwpShowWindow = 0x0040;
        private const uint PmRemove = 0x0001;
        private const uint WmNcHitTest = 0x0084;
        private const uint WmEraseBackground = 0x0014;
        private const int HtTransparent = -1;
        private static readonly IntPtr HwndTopmost = new(-1);
        private static readonly WindowProcedure WindowProc = ProcessMessage;
        private static int _windowId;

        private readonly DirectCompositionTimingHost _owner;
        private readonly IntPtr _window;
        private readonly IDCompositionTarget _target;
        private readonly IDCompositionVisual _visual;
        private IDXGISwapChain1? _swapChain;
        private IDXGISurface? _surface;
        private ID2D1DeviceContext? _drawing;
        private NativeDashboardBounds _bounds;
        private int _width;
        private int _height;
        private bool _visible;
        private readonly List<ID2D1SolidColorBrush> _brushes = [];

        public TimingSurface(DirectCompositionTimingHost owner, string title)
        {
            _owner = owner;
            _window = CreateWindow(title);
            owner._compositionDevice.CreateTargetForHwnd(_window, true, out _target).CheckError();
            owner._compositionDevice.CreateVisual(out _visual).CheckError();
            _target.SetRoot(_visual).CheckError();
        }

        public ID2D1DeviceContext Drawing => _drawing!;
        public ID2D1SolidColorBrush White { get; private set; } = null!;
        public ID2D1SolidColorBrush Muted { get; private set; } = null!;
        public ID2D1SolidColorBrush Header { get; private set; } = null!;
        public ID2D1SolidColorBrush ColumnHeader { get; private set; } = null!;
        public ID2D1SolidColorBrush RowOne { get; private set; } = null!;
        public ID2D1SolidColorBrush RowTwo { get; private set; } = null!;
        public ID2D1SolidColorBrush RelativeOne { get; private set; } = null!;
        public ID2D1SolidColorBrush RelativeTwo { get; private set; } = null!;
        public ID2D1SolidColorBrush Player { get; private set; } = null!;
        public ID2D1SolidColorBrush PlayerLight { get; private set; } = null!;
        public ID2D1SolidColorBrush DarkText { get; private set; } = null!;
        public ID2D1SolidColorBrush ClassRed { get; private set; } = null!;
        public ID2D1SolidColorBrush ClassBlue { get; private set; } = null!;
        public ID2D1SolidColorBrush ClassGreen { get; private set; } = null!;
        public ID2D1SolidColorBrush Gold { get; private set; } = null!;
        public ID2D1SolidColorBrush Amber { get; private set; } = null!;
        public ID2D1SolidColorBrush BrandBlue { get; private set; } = null!;
        public ID2D1SolidColorBrush BrandRed { get; private set; } = null!;
        public ID2D1SolidColorBrush BrandGray { get; private set; } = null!;
        public ID2D1SolidColorBrush BrandGreen { get; private set; } = null!;
        public ID2D1SolidColorBrush BrandYellow { get; private set; } = null!;
        public ID2D1SolidColorBrush BrandOrange { get; private set; } = null!;

        public void Render(
            NativeDashboardBounds bounds,
            bool visible,
            double opacity,
            NativeOverlayStyle style,
            Action<TimingSurface> draw)
        {
            if (!visible || bounds.Width < 64 || bounds.Height < 96)
            {
                if (_visible) ShowWindow(_window, 0);
                _visible = false;
                return;
            }

            EnsureBounds(bounds);
            EnsureSwapChain(bounds.Width, bounds.Height);
            if (!_visible) ShowWindow(_window, 4);
            _visible = true;
            var scale = Math.Min(bounds.Width / 500f, bounds.Height / 410f);
            var offsetX = (bounds.Width - (500 * scale)) / 2f;
            var offsetY = (bounds.Height - (410 * scale)) / 2f;
            _drawing!.Transform = Matrix3x2.CreateScale(scale) * Matrix3x2.CreateTranslation(offsetX, offsetY);
            _drawing.BeginDraw();
            _drawing.Clear(new Color4(0, 0, 0, 0));
            var alpha = (float)Math.Clamp(opacity, 0.15, 1);
            ApplyStyle(style);
            SetBackgroundOpacity(alpha);
            draw(this);
            _drawing.EndDraw().CheckError();
            _swapChain!.Present(1, PresentFlags.None).CheckError();
        }

        private void EnsureBounds(NativeDashboardBounds bounds)
        {
            if (bounds == _bounds) return;
            SetWindowPos(_window, HwndTopmost, bounds.Left, bounds.Top, bounds.Width, bounds.Height, SwpNoActivate | SwpShowWindow);
            _bounds = bounds;
        }

        private void EnsureSwapChain(int width, int height)
        {
            if (_swapChain is not null && _width == width && _height == height) return;
            ReleaseDrawing();
            _swapChain?.Dispose();
            _swapChain = _owner._dxgiFactory.CreateSwapChainForComposition(
                _owner._d3dDevice,
                new SwapChainDescription1
                {
                    Width = (uint)width,
                    Height = (uint)height,
                    Format = Format.B8G8R8A8_UNorm,
                    SampleDescription = new SampleDescription(1, 0),
                    BufferUsage = Usage.RenderTargetOutput,
                    BufferCount = 2,
                    Scaling = Scaling.Stretch,
                    SwapEffect = SwapEffect.FlipSequential,
                    AlphaMode = AlphaMode.Premultiplied,
                });
            _visual.SetContent(_swapChain).CheckError();
            _owner._compositionDevice.Commit().CheckError();
            _surface = _swapChain.GetBuffer<IDXGISurface>(0);
            _drawing = Vortice.Direct2D1.D2D1.D2D1CreateDeviceContext(_surface, null);
            _drawing.SetDpi(96, 96);
            _width = width;
            _height = height;
            CreateBrushes();
        }

        private void CreateBrushes()
        {
            White = Brush(245, 247, 250); Muted = Brush(140, 153, 173);
            Header = Brush(18, 28, 50); ColumnHeader = Brush(23, 33, 59); RowOne = Brush(13, 19, 38); RowTwo = Brush(19, 27, 49);
            RelativeOne = Brush(17, 25, 43); RelativeTwo = Brush(24, 33, 54);
            Player = Brush(14, 92, 116); PlayerLight = Brush(216, 221, 232); DarkText = Brush(24, 31, 44);
            ClassRed = Brush(151, 25, 37); ClassBlue = Brush(24, 43, 78); ClassGreen = Brush(0, 186, 104);
            Gold = Brush(255, 214, 36); Amber = Brush(255, 170, 0);
            BrandBlue = Brush(42, 115, 205); BrandRed = Brush(238, 31, 52); BrandGray = Brush(105, 119, 133);
            BrandGreen = Brush(20, 130, 92); BrandYellow = Brush(205, 163, 20); BrandOrange = Brush(224, 104, 24);
        }

        private void ApplyStyle(NativeOverlayStyle style)
        {
            White.Color = Color(style.PrimaryText);
            Muted.Color = Color(style.SecondaryText);
            Header.Color = Color(style.Background);
            ColumnHeader.Color = Color(style.Card);
            RowOne.Color = Color(style.Background);
            RowTwo.Color = Color(Blend(style.Background, style.Card, 0.55));
            RelativeOne.Color = Color(style.Background);
            RelativeTwo.Color = Color(Blend(style.Background, style.Card, 0.7));
            Player.Color = Color(Blend(style.Background, style.Accent, 0.55));
            PlayerLight.Color = Color(style.PrimaryText);
        }

        private static NativeOverlayColor Blend(
            NativeOverlayColor first,
            NativeOverlayColor second,
            double amount)
        {
            amount = Math.Clamp(amount, 0, 1);
            return new(
                (byte)Math.Round(first.Red + ((second.Red - first.Red) * amount)),
                (byte)Math.Round(first.Green + ((second.Green - first.Green) * amount)),
                (byte)Math.Round(first.Blue + ((second.Blue - first.Blue) * amount)));
        }

        private static Color4 Color(NativeOverlayColor color) => new(
            color.Red / 255f,
            color.Green / 255f,
            color.Blue / 255f,
            1);

        private void SetBackgroundOpacity(float opacity)
        {
            foreach (var brush in new[]
            {
                Header, ColumnHeader, RowOne, RowTwo, RelativeOne, RelativeTwo, Player,
                PlayerLight, ClassRed, ClassBlue, ClassGreen, BrandBlue,
                BrandRed, BrandGray, BrandGreen, BrandYellow, BrandOrange,
            })
            {
                brush.Opacity = opacity;
            }
        }

        private ID2D1SolidColorBrush Brush(byte r, byte g, byte b, byte a = 255)
        {
            var brush = _drawing!.CreateSolidColorBrush(new Color4(r / 255f, g / 255f, b / 255f, a / 255f));
            _brushes.Add(brush);
            return brush;
        }

        private void ReleaseDrawing()
        {
            foreach (var brush in _brushes) brush.Dispose();
            _brushes.Clear();
            _drawing?.Dispose(); _drawing = null;
            _surface?.Dispose(); _surface = null;
        }

        public static void PumpMessages()
        {
            while (PeekMessage(out var message, IntPtr.Zero, 0, 0, PmRemove))
            {
                TranslateMessage(ref message);
                DispatchMessage(ref message);
            }
        }

        public void Dispose()
        {
            ReleaseDrawing();
            _swapChain?.Dispose();
            _visual.Dispose();
            _target.Dispose();
            DestroyWindow(_window);
        }

        private static IntPtr CreateWindow(string title)
        {
            var className = $"LmuOverlay.Timing.{Environment.ProcessId}.{Interlocked.Increment(ref _windowId)}";
            var instance = GetModuleHandle(null);
            var windowClass = new WindowClass
            {
                Size = (uint)Marshal.SizeOf<WindowClass>(), Instance = instance, ClassName = className,
                WindowProcedure = Marshal.GetFunctionPointerForDelegate(WindowProc),
            };
            if (RegisterClassEx(ref windowClass) == 0) throw new InvalidOperationException($"RegisterClassEx failed: {Marshal.GetLastWin32Error()}.");
            var window = CreateWindowEx(WsExTopmost | WsExTransparent | WsExToolWindow | WsExNoActivate | WsExNoRedirectionBitmap,
                className, title, WsPopup, 0, 0, 1, 1, IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);
            if (window == IntPtr.Zero) throw new InvalidOperationException($"CreateWindowEx failed: {Marshal.GetLastWin32Error()}.");
            return window;
        }

        private static IntPtr ProcessMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam) => message switch
        {
            WmNcHitTest => new IntPtr(HtTransparent),
            WmEraseBackground => new IntPtr(1),
            _ => DefWindowProc(window, message, wParam, lParam),
        };

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WindowClass
        {
            public uint Size, Style; public IntPtr WindowProcedure; public int ClassExtra, WindowExtra;
            public IntPtr Instance, Icon, Cursor, Background; public string? MenuName, ClassName; public IntPtr SmallIcon;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeMessage
        {
            public IntPtr Window; public uint Message; public IntPtr WParam, LParam; public uint Time;
            public int PointX, PointY; public uint Private;
        }
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr WindowProcedure(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ushort RegisterClassEx(ref WindowClass windowClass);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateWindowEx(uint extendedStyle, string className, string windowName, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
        [DllImport("user32.dll")] private static extern IntPtr DefWindowProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] private static extern bool PeekMessage(out NativeMessage message, IntPtr window, uint filterMinimum, uint filterMaximum, uint removeMessage);
        [DllImport("user32.dll")] private static extern bool TranslateMessage(ref NativeMessage message);
        [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref NativeMessage message);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? moduleName);
    }
}
