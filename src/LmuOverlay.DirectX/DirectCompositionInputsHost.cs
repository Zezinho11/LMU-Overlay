using System.Drawing;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using SharpGen.Runtime;
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

internal sealed unsafe class DirectCompositionInputsHost : IDisposable
{
    private const int DesignWidth = 520;
    private const int DesignHeight = 220;
    private const int SteeringWheelPixels = 512;
    private const string SteeringWheelResource =
        "LmuOverlay.DirectX.Assets.steering-wheel-bgra.bin";
    private const uint WsPopup = 0x80000000;
    private const uint WsExTopmost = 0x00000008;
    private const uint WsExTransparent = 0x00000020;
    private const uint WsExToolWindow = 0x00000080;
    private const uint WsExNoActivate = 0x08000000;
    private const uint WsExNoRedirectionBitmap = 0x00200000;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const uint PmRemove = 0x0001;
    private const uint WmDestroy = 0x0002;
    private const uint WmEraseBackground = 0x0014;
    private const uint WmNcHitTest = 0x0084;
    private const int HtTransparent = -1;
    private static readonly IntPtr HwndTopmost = new(-1);
    private static readonly WindowProcedure WindowProc = ProcessWindowMessage;

    private readonly IntPtr _window;
    private readonly ID3D11Device _d3dDevice;
    private readonly ID3D11DeviceContext _d3dContext;
    private readonly IDXGIDevice _dxgiDevice;
    private readonly IDXGIFactory2 _dxgiFactory;
    private readonly IDCompositionDevice _compositionDevice;
    private readonly IDCompositionTarget _compositionTarget;
    private readonly IDCompositionVisual _compositionVisual;
    private readonly IDWriteFactory _writeFactory;
    private readonly Dictionary<float, IDWriteTextFormat> _textFormats = [];
    private readonly InputSample[] _history = new InputSample[768];
    private IDXGISwapChain1? _swapChain;
    private IDXGISurface? _surface;
    private ID2D1DeviceContext? _drawing;
    private ID2D1SolidColorBrush? _white;
    private ID2D1SolidColorBrush? _muted;
    private ID2D1SolidColorBrush? _green;
    private ID2D1SolidColorBrush? _red;
    private ID2D1SolidColorBrush? _amber;
    private ID2D1SolidColorBrush? _cyan;
    private ID2D1SolidColorBrush? _panel;
    private ID2D1SolidColorBrush? _border;
    private ID2D1Bitmap1? _steeringWheel;
    private NativeDashboardBounds _bounds;
    private int _surfaceWidth;
    private int _surfaceHeight;
    private int _head;
    private int _count;
    private long _renderedSequence = -1;
    private bool _visible;
    private string _sessionKey = string.Empty;

    public DirectCompositionInputsHost()
    {
        _window = CreateOverlayWindow();
        var featureLevels = new[]
        {
            Vortice.Direct3D.FeatureLevel.Level_11_1,
            Vortice.Direct3D.FeatureLevel.Level_11_0,
            Vortice.Direct3D.FeatureLevel.Level_10_1,
            Vortice.Direct3D.FeatureLevel.Level_10_0,
        };
        D3D11CreateDevice(
            IntPtr.Zero,
            DriverType.Hardware,
            DeviceCreationFlags.BgraSupport,
            featureLevels,
            out _d3dDevice,
            out _d3dContext).CheckError();
        _dxgiDevice = _d3dDevice.QueryInterface<IDXGIDevice>();
        _dxgiFactory = CreateDXGIFactory2<IDXGIFactory2>(false);
        _compositionDevice = DCompositionCreateDevice<IDCompositionDevice>(_dxgiDevice);
        _compositionDevice.CreateTargetForHwnd(_window, true, out _compositionTarget).CheckError();
        _compositionDevice.CreateVisual(out _compositionVisual).CheckError();
        _compositionTarget.SetRoot(_compositionVisual).CheckError();
        _writeFactory = DWriteCreateFactory<IDWriteFactory>();
    }

    public void Render(NativeInputsFrame frame)
    {
        if (frame.Sequence == _renderedSequence)
        {
            return;
        }
        if (!frame.Visible || frame.Bounds.Width < 100 || frame.Bounds.Height < 60)
        {
            if (_visible)
            {
                ShowWindow(_window, 0);
                _visible = false;
            }
            _renderedSequence = frame.Sequence;
            return;
        }

        EnsureBounds(frame.Bounds);
        if (!_visible)
        {
            ShowWindow(_window, 4);
            _visible = true;
        }
        EnsureSwapChain(frame.Bounds.Width, frame.Bounds.Height);
        ApplyStyle(frame.Style ?? NativeOverlayStyle.RedFox);
        if (!string.Equals(_sessionKey, frame.SessionKey, StringComparison.Ordinal))
        {
            _sessionKey = frame.SessionKey;
            _head = 0;
            _count = 0;
        }
        Capture(frame);
        Draw(frame);
        _renderedSequence = frame.Sequence;
    }

    public void PumpMessages()
    {
        while (PeekMessage(out var message, IntPtr.Zero, 0, 0, PmRemove))
        {
            TranslateMessage(ref message);
            DispatchMessage(ref message);
        }
    }

    private void EnsureBounds(NativeDashboardBounds bounds)
    {
        if (bounds == _bounds) return;
        SetWindowPos(_window, HwndTopmost, bounds.Left, bounds.Top, bounds.Width, bounds.Height,
            SwpNoActivate | SwpShowWindow);
        _bounds = bounds;
    }

    private void EnsureSwapChain(int width, int height)
    {
        if (_swapChain is not null && _surface is not null && _drawing is not null &&
            _surfaceWidth == width && _surfaceHeight == height)
        {
            return;
        }
        ReleaseDrawingResources();
        _swapChain?.Dispose();
        _swapChain = _dxgiFactory.CreateSwapChainForComposition(
            _d3dDevice,
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
            },
            null);
        _compositionVisual.SetContent(_swapChain).CheckError();
        _compositionDevice.Commit().CheckError();
        _surface = _swapChain.GetBuffer<IDXGISurface>(0);
        _drawing = D2D1CreateDeviceContext(_surface, null);
        _drawing.SetDpi(96, 96);
        _surfaceWidth = width;
        _surfaceHeight = height;
        CreateDrawingResources();
    }

    private void CreateDrawingResources()
    {
        var drawing = _drawing ?? throw new InvalidOperationException("Direct2D context is unavailable.");
        _white = drawing.CreateSolidColorBrush(Color(245, 247, 250));
        _muted = drawing.CreateSolidColorBrush(Color(116, 132, 148));
        _green = drawing.CreateSolidColorBrush(Color(52, 222, 130));
        _red = drawing.CreateSolidColorBrush(Color(247, 66, 77));
        _amber = drawing.CreateSolidColorBrush(Color(255, 190, 64));
        _cyan = drawing.CreateSolidColorBrush(Color(18, 217, 229));
        _panel = drawing.CreateSolidColorBrush(Color(5, 8, 10, 238));
        _border = drawing.CreateSolidColorBrush(Color(66, 211, 166));
        _steeringWheel = LoadSteeringWheel(drawing);
    }

    private void ApplyStyle(NativeOverlayStyle style)
    {
        _white!.Color = Color(style.PrimaryText);
        _muted!.Color = Color(style.SecondaryText);
        _green!.Color = Color(style.Positive);
        _red!.Color = Color(style.Critical);
        _amber!.Color = Color(style.Attention);
        _cyan!.Color = Color(style.Information);
        _panel!.Color = Color(style.Background);
        _panel.Opacity = (float)Math.Clamp(style.BackgroundOpacity, 0.2, 1);
        _border!.Color = Color(style.Accent);
    }

    private static ID2D1Bitmap1? LoadSteeringWheel(ID2D1DeviceContext drawing)
    {
        try
        {
            using var stream = typeof(DirectCompositionInputsHost).Assembly
                .GetManifestResourceStream(SteeringWheelResource);
            if (stream is null || stream.Length !=
                SteeringWheelPixels * SteeringWheelPixels * 4)
            {
                return null;
            }

            var pixels = new byte[stream.Length];
            stream.ReadExactly(pixels);
            fixed (byte* source = pixels)
            {
                return drawing.CreateBitmap(
                    new SizeI(SteeringWheelPixels, SteeringWheelPixels),
                    (IntPtr)source,
                    SteeringWheelPixels * 4,
                    new BitmapProperties1(
                        new Vortice.DCommon.PixelFormat(
                            Format.B8G8R8A8_UNorm,
                            Vortice.DCommon.AlphaMode.Premultiplied)));
            }
        }
        catch
        {
            // Preserve the code-native steering indicator if the optional
            // embedded artwork cannot be decoded or uploaded to Direct2D.
            return null;
        }
    }

    private void Capture(NativeInputsFrame frame)
    {
        if (!frame.Inputs.Available)
        {
            _head = 0;
            _count = 0;
            return;
        }
        _history[_head] = new(
            frame.CapturedTimestamp,
            (float)Math.Clamp(frame.Inputs.Throttle, 0, 1),
            (float)Math.Clamp(frame.Inputs.Brake, 0, 1),
            frame.Inputs.AbsActive);
        _head = (_head + 1) % _history.Length;
        _count = Math.Min(_history.Length, _count + 1);
    }

    private void Draw(NativeInputsFrame frame)
    {
        var drawing = _drawing ?? throw new InvalidOperationException("Direct2D context is unavailable.");
        var scale = Math.Min(frame.Bounds.Width / (float)DesignWidth, frame.Bounds.Height / (float)DesignHeight);
        var offsetX = (frame.Bounds.Width - DesignWidth * scale) / 2f;
        var offsetY = (frame.Bounds.Height - DesignHeight * scale) / 2f;
        drawing.Transform = Matrix3x2.CreateScale(scale) * Matrix3x2.CreateTranslation(offsetX, offsetY);
        drawing.BeginDraw();
        drawing.Clear(new Color4(0, 0, 0, 0));
        FillRounded(drawing, 2, 2, 516, 216, 12, _panel!);
        DrawRounded(drawing, 2, 2, 516, 216, 12, _border!, 2);
        DrawText(drawing, "DRIVER INPUTS", 18, 12, 220, 28, 17, _white!);
        DrawSteering(drawing, frame.Inputs.Steering);
        DrawGraph(drawing);
        DrawText(drawing, $"THR {frame.Inputs.Throttle:P0}", 172, 184, 82, 22, 13, _green!);
        DrawText(drawing, $"BRK {frame.Inputs.Brake:P0}", 258, 184, 82, 22, 13,
            frame.Inputs.AbsActive ? _amber! : _red!);
        DrawText(drawing, $"CLU {frame.Inputs.Clutch:P0}", 344, 184, 82, 22, 13, _cyan!);
        DrawText(drawing, $"STR {frame.Inputs.Steering:+0%;-0%;0%}", 430, 184, 72, 22, 12, _white!, TextAlignment.Trailing);
        if (frame.Inputs.AbsActive)
        {
            var blinkOn = (frame.CapturedTimestamp / Math.Max(1, Stopwatch.Frequency / 8)) % 2 == 0;
            FillRounded(drawing, 438, 13, 64, 25, 4, blinkOn ? _amber! : _red!);
            DrawText(drawing, "ABS", 438, 14, 64, 22, 13, _panel!, TextAlignment.Center);
        }
        drawing.EndDraw().CheckError();
        _swapChain!.Present(1, PresentFlags.None).CheckError();
    }

    private void DrawSteering(ID2D1DeviceContext drawing, double steering)
    {
        var center = new Vector2(88, 112);
        var angle = (float)(Math.Clamp(steering, -1, 1) * Math.PI * 0.78);
        if (_steeringWheel is not null)
        {
            drawing.FillEllipse(new Ellipse(center, 56, 56), _muted!);
            drawing.DrawEllipse(new Ellipse(center, 57, 57), _cyan!, 1.5f);
            var transform = drawing.Transform;
            drawing.Transform = Matrix3x2.CreateRotation(angle, center) * transform;
            drawing.DrawBitmap(
                _steeringWheel,
                new Vortice.RawRectF(30, 54, 146, 170),
                1,
                Vortice.Direct2D1.InterpolationMode.Linear,
                null,
                null);
            drawing.Transform = transform;
            DrawText(drawing, "STEERING", 34, 169, 108, 20, 11, _muted!, TextAlignment.Center);
            return;
        }

        const float radius = 49;
        drawing.DrawEllipse(new Ellipse(center, radius, radius), _white!, 5);
        for (var index = 0; index < 3; index++)
        {
            var spoke = angle + (float)(index * Math.PI * 2 / 3);
            var end = center + new Vector2(MathF.Cos(spoke), MathF.Sin(spoke)) * 42;
            drawing.DrawLine(center, end, _cyan!, 5);
        }
        drawing.FillEllipse(new Ellipse(center, 10, 10), _white!);
        DrawText(drawing, "STEERING", 34, 169, 108, 20, 11, _muted!, TextAlignment.Center);
    }

    private void DrawGraph(ID2D1DeviceContext drawing)
    {
        const float left = 172;
        const float top = 48;
        const float width = 330;
        const float height = 125;
        DrawRounded(drawing, left, top, width, height, 3, _muted!, 1);
        for (var row = 1; row < 4; row++)
        {
            var y = top + height * row / 4;
            drawing.DrawLine(new Vector2(left, y), new Vector2(left + width, y), _muted!, 0.5f);
        }
        if (_count < 2) return;
        var newestIndex = (_head - 1 + _history.Length) % _history.Length;
        var newest = _history[newestIndex].Timestamp;
        var oldest = newest - Stopwatch.Frequency * 4;
        var hasPrevious = false;
        Vector2 previousThrottle = default;
        Vector2 previousBrake = default;
        for (var offset = _count - 1; offset >= 0; offset--)
        {
            var sample = _history[(_head - 1 - offset + _history.Length) % _history.Length];
            if (sample.Timestamp < oldest) continue;
            var x = left + (float)((sample.Timestamp - oldest) /
                (double)(Stopwatch.Frequency * 4)) * width;
            var throttle = new Vector2(x, top + height - sample.Throttle * height);
            var brake = new Vector2(x, top + height - sample.Brake * height);
            if (hasPrevious)
            {
                drawing.DrawLine(previousThrottle, throttle, _green!, 2);
                drawing.DrawLine(previousBrake, brake, sample.AbsActive ? _amber! : _red!,
                    sample.AbsActive ? 3 : 2);
            }
            previousThrottle = throttle;
            previousBrake = brake;
            hasPrevious = true;
        }
    }

    private static void FillRounded(ID2D1DeviceContext drawing, float x, float y, float width, float height,
        float radius, ID2D1Brush brush) => drawing.FillRoundedRectangle(
        new RoundedRectangle(new RectangleF(x, y, width, height), radius, radius), brush);

    private static void DrawRounded(ID2D1DeviceContext drawing, float x, float y, float width, float height,
        float radius, ID2D1Brush brush, float stroke) => drawing.DrawRoundedRectangle(
        new RoundedRectangle(new RectangleF(x, y, width, height), radius, radius), brush, stroke);

    private void DrawText(ID2D1DeviceContext drawing, string value, float x, float y, float width, float height,
        float size, ID2D1Brush brush, TextAlignment alignment = TextAlignment.Leading)
    {
        var format = GetTextFormat(size);
        format.TextAlignment = alignment;
        drawing.DrawText(value, format, new Rect(x, y, width, height), brush);
    }

    private IDWriteTextFormat GetTextFormat(float size)
    {
        if (_textFormats.TryGetValue(size, out var existing)) return existing;
        var format = _writeFactory.CreateTextFormat("Bahnschrift", null, FontWeight.SemiBold,
            FontStyle.Normal, FontStretch.Normal, size, "pt-BR");
        format.ParagraphAlignment = ParagraphAlignment.Center;
        _textFormats.Add(size, format);
        return format;
    }

    private static Color4 Color(byte red, byte green, byte blue, byte alpha = 255) =>
        new(red / 255f, green / 255f, blue / 255f, alpha / 255f);

    private static Color4 Color(NativeOverlayColor color) =>
        Color(color.Red, color.Green, color.Blue);

    private readonly record struct InputSample(
        long Timestamp,
        float Throttle,
        float Brake,
        bool AbsActive);

    private void ReleaseDrawingResources()
    {
        _steeringWheel?.Dispose(); _steeringWheel = null;
        _white?.Dispose(); _white = null;
        _muted?.Dispose(); _muted = null;
        _green?.Dispose(); _green = null;
        _red?.Dispose(); _red = null;
        _amber?.Dispose(); _amber = null;
        _cyan?.Dispose(); _cyan = null;
        _panel?.Dispose(); _panel = null;
        _border?.Dispose(); _border = null;
        _drawing?.Dispose(); _drawing = null;
        _surface?.Dispose(); _surface = null;
    }

    public void Dispose()
    {
        ReleaseDrawingResources();
        foreach (var format in _textFormats.Values) format.Dispose();
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
        var className = $"LmuOverlay.DirectX.Inputs.{Environment.ProcessId}";
        var instance = GetModuleHandle(null);
        var windowClass = new WindowClass
        {
            Size = (uint)Marshal.SizeOf<WindowClass>(),
            Instance = instance,
            ClassName = className,
            WindowProcedure = Marshal.GetFunctionPointerForDelegate(WindowProc),
        };
        if (RegisterClassEx(ref windowClass) == 0)
            throw new InvalidOperationException($"RegisterClassEx failed: {Marshal.GetLastWin32Error()}.");
        var window = CreateWindowEx(
            WsExTopmost | WsExTransparent | WsExToolWindow | WsExNoActivate | WsExNoRedirectionBitmap,
            className, "LMU DirectX Inputs", WsPopup, 0, 0, 1, 1,
            IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);
        if (window == IntPtr.Zero)
            throw new InvalidOperationException($"CreateWindowEx failed: {Marshal.GetLastWin32Error()}.");
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
        public uint Size; public uint Style; public IntPtr WindowProcedure; public int ClassExtra;
        public int WindowExtra; public IntPtr Instance; public IntPtr Icon; public IntPtr Cursor;
        public IntPtr Background; public string? MenuName; public string? ClassName; public IntPtr SmallIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public IntPtr Window; public uint Message; public IntPtr WParam; public IntPtr LParam;
        public uint Time; public int PointX; public int PointY; public uint Private;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr WindowProcedure(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassEx(ref WindowClass windowClass);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(uint extendedStyle, string className, string windowName, uint style,
        int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
    [DllImport("user32.dll")] private static extern IntPtr DefWindowProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out NativeMessage message, IntPtr window,
        uint filterMinimum, uint filterMaximum, uint removeMessage);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref NativeMessage message);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref NativeMessage message);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? moduleName);
}
