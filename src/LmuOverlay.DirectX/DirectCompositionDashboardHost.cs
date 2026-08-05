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

internal sealed class DirectCompositionDashboardHost : IDisposable
{
    private const int DesignWidth = 800;
    private const int DesignHeight = 480;
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
    private IDXGISwapChain1? _swapChain;
    private IDXGISurface? _surface;
    private ID2D1DeviceContext? _drawing;
    private ID2D1SolidColorBrush? _white;
    private ID2D1SolidColorBrush? _muted;
    private ID2D1SolidColorBrush? _green;
    private ID2D1SolidColorBrush? _cyan;
    private ID2D1SolidColorBrush? _amber;
    private ID2D1SolidColorBrush? _red;
    private ID2D1SolidColorBrush? _blue;
    private ID2D1SolidColorBrush? _panel;
    private ID2D1SolidColorBrush? _border;
    private NativeDashboardBounds _bounds;
    private int _surfaceWidth;
    private int _surfaceHeight;
    private long _renderedSequence = -1;
    private bool _visible;
    private readonly PedalSample[] _pedalHistory = new PedalSample[512];
    private int _pedalHead;
    private int _pedalCount;

    public DirectCompositionDashboardHost()
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
        _compositionDevice.CreateTargetForHwnd(
            _window,
            true,
            out _compositionTarget).CheckError();
        _compositionDevice.CreateVisual(out _compositionVisual).CheckError();
        _compositionTarget.SetRoot(_compositionVisual).CheckError();
        _writeFactory = DWriteCreateFactory<IDWriteFactory>();
    }

    public void Render(NativeDashboardFrame frame)
    {
        if (frame.Sequence == _renderedSequence)
        {
            return;
        }

        if (!frame.Visible || frame.Bounds.Width < 64 || frame.Bounds.Height < 48)
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
        CapturePedals(frame);
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
        if (bounds == _bounds)
        {
            return;
        }

        SetWindowPos(
            _window,
            HwndTopmost,
            bounds.Left,
            bounds.Top,
            bounds.Width,
            bounds.Height,
            SwpNoActivate | SwpShowWindow);
        _bounds = bounds;
    }

    private void EnsureSwapChain(int width, int height)
    {
        if (_swapChain is not null &&
            _surface is not null &&
            _drawing is not null &&
            _surfaceWidth == width &&
            _surfaceHeight == height)
        {
            return;
        }

        ReleaseDrawingResources();
        _swapChain?.Dispose();
        var description = new SwapChainDescription1
        {
            Width = (uint)width,
            Height = (uint)height,
            Format = Format.B8G8R8A8_UNorm,
            Stereo = false,
            SampleDescription = new SampleDescription(1, 0),
            BufferUsage = Usage.RenderTargetOutput,
            BufferCount = 2,
            Scaling = Scaling.Stretch,
            SwapEffect = SwapEffect.FlipSequential,
            AlphaMode = AlphaMode.Premultiplied,
        };
        _swapChain = _dxgiFactory.CreateSwapChainForComposition(
            _d3dDevice,
            description,
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
        _muted = drawing.CreateSolidColorBrush(Color(153, 166, 180));
        _green = drawing.CreateSolidColorBrush(Color(56, 218, 158));
        _cyan = drawing.CreateSolidColorBrush(Color(18, 217, 229));
        _amber = drawing.CreateSolidColorBrush(Color(245, 174, 54));
        _red = drawing.CreateSolidColorBrush(Color(247, 66, 77));
        _blue = drawing.CreateSolidColorBrush(Color(66, 111, 255));
        _panel = drawing.CreateSolidColorBrush(Color(5, 8, 10, 238));
        _border = drawing.CreateSolidColorBrush(Color(66, 211, 166));
    }

    private void Draw(NativeDashboardFrame frame)
    {
        var drawing = _drawing ?? throw new InvalidOperationException("Direct2D context is unavailable.");
        var dashboard = frame.Dashboard;
        var scale = Math.Min(
            frame.Bounds.Width / (float)DesignWidth,
            frame.Bounds.Height / (float)DesignHeight);
        var offsetX = (frame.Bounds.Width - (DesignWidth * scale)) / 2f;
        var offsetY = (frame.Bounds.Height - (DesignHeight * scale)) / 2f;
        drawing.Transform = Matrix3x2.CreateScale(scale) *
            Matrix3x2.CreateTranslation(offsetX, offsetY);
        drawing.BeginDraw();
        drawing.Clear(new Color4(0, 0, 0, 0));

        FillRounded(drawing, 3, 3, 794, 474, 18, _panel!);
        DrawRounded(drawing, 3, 3, 794, 474, 18, _border!, 3);
        DrawDashboard(drawing, dashboard);

        drawing.EndDraw().CheckError();
        _swapChain!.Present(1, PresentFlags.None).CheckError();
    }

    private void DrawDashboard(ID2D1DeviceContext drawing, LmuOverlay.Widgets.DashboardWidgetState dashboard)
    {
        DrawText(drawing, dashboard.TrackName.ToUpperInvariant(), 42, 54, 210, 20, 11, _muted!);
        DrawText(drawing, "REDFOX RACING", 236, 34, 328, 36, 26, _white!, TextAlignment.Center);
        DrawText(drawing, dashboard.SessionName, 606, 54, 152, 20, 11, _muted!, TextAlignment.Trailing);
        DrawShiftLights(drawing, dashboard.EngineRpmFraction);
        DrawPanel(drawing, 42, 86, 225, 176);
        DrawPanel(drawing, 276, 86, 248, 176);
        DrawPanel(drawing, 533, 86, 225, 176);
        DrawPanel(drawing, 42, 272, 225, 164);
        DrawPanel(drawing, 276, 272, 248, 164);
        DrawPanel(drawing, 533, 272, 225, 164);

        DrawText(drawing, dashboard.Available ? $"POS {dashboard.Position}" : "POS --", 56, 104, 120, 24, 17, _green!);
        DrawText(drawing, dashboard.Available ? $"LAP {dashboard.LapNumber}" : "LAP --", 56, 130, 120, 22, 15, _white!);
        DrawText(drawing, dashboard.Available ? $"DELTA {dashboard.DeltaBestSeconds:+0.000;-0.000;0.000}" : "DELTA --", 56, 156, 178, 22, 15, _amber!);
        DrawText(drawing, dashboard.Available ? $"FUEL {dashboard.FuelLiters:0.0} L" : "FUEL --.- L", 56, 184, 160, 20, 13, _muted!);
        DrawText(drawing, dashboard.Available ? $"VIRTUAL ENERGY {dashboard.VirtualEnergyFraction:P0}" : "VIRTUAL ENERGY --", 56, 207, 190, 20, 13, _cyan!);
        DrawText(drawing, dashboard.Available ? $"BRAKE BIAS {(1 - dashboard.RearBrakeBiasFraction):P1}" : "BRAKE BIAS --", 56, 230, 180, 20, 13, _muted!);

        DrawText(drawing, dashboard.Available ? $"{dashboard.SpeedKilometersPerHour:0} KM/H" : "--- KM/H", 296, 98, 208, 34, 23, _muted!, TextAlignment.Center);
        DrawText(drawing, dashboard.Available ? dashboard.Gear : "N", 306, 125, 188, 96, 74, _white!, TextAlignment.Center);
        DrawText(drawing, dashboard.Available ? $"RPM {dashboard.EngineRpm:0}" : "RPM ----", 306, 224, 188, 24, 15, _muted!, TextAlignment.Center);
        if (dashboard.SpeedLimiterActive)
        {
            FillRounded(drawing, 448, 98, 58, 24, 4, _amber!);
            DrawText(drawing, "LIMIT", 448, 99, 58, 22, 12, _panel!, TextAlignment.Center);
        }

        DrawText(drawing, $"CURRENT {FormatLap(dashboard.CurrentLapTimeSeconds)}", 548, 100, 190, 20, 13, _white!);
        DrawText(drawing, $"LAST {FormatLap(dashboard.LastLapTimeSeconds)}", 548, 123, 190, 20, 13, _muted!);
        DrawText(drawing, $"BEST {FormatLap(dashboard.BestLapTimeSeconds)}", 548, 146, 190, 20, 13, _green!);
        DrawText(drawing, $"OPTIMAL {FormatLap(dashboard.OptimalLapTimeSeconds)}", 548, 169, 190, 20, 13, _cyan!);
        DrawControlCards(drawing, dashboard);
        DrawText(drawing, $"OIL {dashboard.EngineOilTemperatureCelsius:0}°  WATER {dashboard.EngineWaterTemperatureCelsius:0}°", 548, 242, 190, 16, 11, _muted!);

        DrawText(drawing, "SECTORS", 42, 276, 225, 23, 13, _cyan!, TextAlignment.Center);
        DrawText(drawing, "TYRE TEMP / WEAR", 276, 276, 248, 23, 13, _amber!, TextAlignment.Center);
        DrawText(drawing, "PEDAL INPUTS", 533, 276, 225, 23, 13, _cyan!, TextAlignment.Center);
        DrawSectors(drawing, dashboard);
        DrawTires(drawing, dashboard);
        DrawPedals(drawing, dashboard);
        DrawText(drawing, $"THR {dashboard.Throttle:P0}", 548, 309, 90, 20, 13, _green!);
        DrawText(drawing, $"BRK {dashboard.Brake:P0}", 648, 309, 90, 20, 13, _red!);
        DrawText(drawing, $"GX {dashboard.LateralAccelerationG:+0.0;-0.0;0.0}", 548, 406, 90, 20, 12, _muted!);
        DrawText(drawing, $"GY {dashboard.LongitudinalAccelerationG:+0.0;-0.0;0.0}", 648, 406, 90, 20, 12, _muted!);
    }

    private void DrawSectors(ID2D1DeviceContext drawing, LmuOverlay.Widgets.DashboardWidgetState dashboard)
    {
        var sectors = dashboard.SectorTimes;
        var values = new[]
        {
            ("S1", sectors.CurrentSector1Seconds, sectors.LastSector1Seconds, sectors.BestSector1Seconds),
            ("S2", sectors.CurrentSector2Seconds, sectors.LastSector2Seconds, sectors.BestSector2Seconds),
            ("S3", sectors.CurrentSector3Seconds, sectors.LastSector3Seconds, sectors.BestSector3Seconds),
        };
        for (var index = 0; index < values.Length; index++)
        {
            var value = values[index].Item2 > 0 ? values[index].Item2 : values[index].Item3;
            var y = 314 + (index * 34);
            DrawText(drawing, values[index].Item1, 58, y, 32, 22, 14, _white!);
            DrawText(drawing, value > 0 ? $"{value:0.000}" : "--.---", 96, y, 76, 22, 14, _white!, TextAlignment.Trailing);
            var delta = value > 0 && values[index].Item4 > 0
                ? value - values[index].Item4
                : double.NaN;
            DrawText(
                drawing,
                double.IsFinite(delta) ? delta.ToString("+0.000;-0.000;0.000") : "--.---",
                178,
                y,
                68,
                22,
                12,
                delta <= 0 ? _green! : _red!,
                TextAlignment.Trailing);
        }
        DrawText(
            drawing,
            $"AIR {dashboard.AmbientTemperatureCelsius:0}°  TRACK {dashboard.TrackTemperatureCelsius:0}°  RAIN {dashboard.RainIntensity:P0}",
            58,
            410,
            188,
            18,
            10,
            _muted!);
    }

    private void DrawShiftLights(ID2D1DeviceContext drawing, double rpmFraction)
    {
        var active = (int)Math.Ceiling(Math.Clamp((rpmFraction - 0.65) / 0.35, 0, 1) * 12);
        for (var index = 0; index < 12; index++)
        {
            var brush = index < active
                ? index < 4 ? _green! : index < 7 ? _amber! : index < 10 ? _red! : _blue!
                : _muted!;
            drawing.FillEllipse(new Ellipse(new Vector2(323 + (index * 14), 22), 5, 5), brush);
        }
    }

    private void DrawControlCards(ID2D1DeviceContext drawing, LmuOverlay.Widgets.DashboardWidgetState dashboard)
    {
        var values = new[]
        {
            ("TC", dashboard.TractionControlLevel, dashboard.TractionControlMaximum),
            ("SLIP", dashboard.TractionControlSlipLevel, dashboard.TractionControlSlipMaximum),
            ("CUT", dashboard.TractionControlCutLevel, dashboard.TractionControlCutMaximum),
            ("ABS", dashboard.AbsLevel, dashboard.AbsMaximum),
        };
        for (var index = 0; index < values.Length; index++)
        {
            var x = 548 + (index * 48);
            FillRounded(drawing, x, 195, 43, 42, 4, _green!);
            DrawText(drawing, values[index].Item1, x, 197, 43, 14, 9, _panel!, TextAlignment.Center);
            DrawText(drawing, values[index].Item3 > 0 ? values[index].Item2.ToString() : "--", x, 211, 43, 21, 15, _white!, TextAlignment.Center);
        }
    }

    private void DrawTires(ID2D1DeviceContext drawing, LmuOverlay.Widgets.DashboardWidgetState dashboard)
    {
        var tires = new[]
        {
            ("FL", dashboard.TireTemperatures.FrontLeftCelsius, dashboard.TireWear.FrontLeftFraction, 294f, 316f),
            ("FR", dashboard.TireTemperatures.FrontRightCelsius, dashboard.TireWear.FrontRightFraction, 408f, 316f),
            ("RL", dashboard.TireTemperatures.RearLeftCelsius, dashboard.TireWear.RearLeftFraction, 294f, 368f),
            ("RR", dashboard.TireTemperatures.RearRightCelsius, dashboard.TireWear.RearRightFraction, 408f, 368f),
        };
        foreach (var tire in tires)
        {
            var color = TireBrush(tire.Item2);
            FillRounded(drawing, tire.Item4, tire.Item5 + 4, 12, 28, 5, color);
            DrawText(drawing, $"{tire.Item1}  {tire.Item2:0}° · {tire.Item3:P0}", tire.Item4 + 18, tire.Item5, 94, 34, 13, _white!);
        }
        DrawText(drawing, $"COMPOUND {dashboard.TireCompound.ToUpperInvariant()}", 294, 414, 212, 18, 11, _amber!, TextAlignment.Center);
    }

    private ID2D1SolidColorBrush TireBrush(double temperature) =>
        temperature < 60 ? _blue! : temperature < 75 ? _cyan! : temperature < 100 ? _green! : temperature < 115 ? _amber! : _red!;

    private void DrawPedals(ID2D1DeviceContext drawing, LmuOverlay.Widgets.DashboardWidgetState dashboard)
    {
        const float left = 548;
        const float top = 338;
        const float width = 190;
        const float height = 62;
        DrawRounded(drawing, left, top, width, height, 3, _muted!, 1);
        for (var row = 1; row < 4; row++)
        {
            var y = top + (height * row / 4);
            drawing.DrawLine(new Vector2(left, y), new Vector2(left + width, y), _muted!, 0.5f);
        }

        if (_pedalCount < 2)
        {
            return;
        }

        var newestIndex = (_pedalHead - 1 + _pedalHistory.Length) % _pedalHistory.Length;
        var newestTime = _pedalHistory[newestIndex].Timestamp;
        var oldestTime = newestTime - Stopwatch.Frequency * 4;
        var hasPrevious = false;
        Vector2 previousThrottle = default;
        Vector2 previousBrake = default;
        for (var offset = _pedalCount - 1; offset >= 0; offset--)
        {
            var index = (_pedalHead - 1 - offset + _pedalHistory.Length) % _pedalHistory.Length;
            var sample = _pedalHistory[index];
            if (sample.Timestamp < oldestTime)
            {
                continue;
            }

            var fraction = (sample.Timestamp - oldestTime) /
                (double)(Stopwatch.Frequency * 4);
            var x = left + ((float)fraction * width);
            var throttlePoint = new Vector2(x, top + height - (sample.Throttle * height));
            var brakePoint = new Vector2(x, top + height - (sample.Brake * height));
            if (hasPrevious)
            {
                drawing.DrawLine(previousThrottle, throttlePoint, _green!, 2);
                drawing.DrawLine(previousBrake, brakePoint, _red!, 2);
            }

            previousThrottle = throttlePoint;
            previousBrake = brakePoint;
            hasPrevious = true;
        }
    }

    private void CapturePedals(NativeDashboardFrame frame)
    {
        if (!frame.Dashboard.Available)
        {
            _pedalHead = 0;
            _pedalCount = 0;
            return;
        }

        _pedalHistory[_pedalHead] = new(
            frame.CapturedTimestamp,
            (float)Math.Clamp(frame.Dashboard.Throttle, 0, 1),
            (float)Math.Clamp(frame.Dashboard.Brake, 0, 1));
        _pedalHead = (_pedalHead + 1) % _pedalHistory.Length;
        _pedalCount = Math.Min(_pedalHistory.Length, _pedalCount + 1);
    }

    private void DrawPanel(ID2D1DeviceContext drawing, float x, float y, float width, float height)
    {
        DrawRounded(drawing, x, y, width, height, 3, _muted!, 1);
    }

    private static void FillRounded(ID2D1DeviceContext drawing, float x, float y, float width, float height, float radius, ID2D1Brush brush) =>
        drawing.FillRoundedRectangle(new RoundedRectangle(new RectangleF(x, y, width, height), radius, radius), brush);

    private static void DrawRounded(ID2D1DeviceContext drawing, float x, float y, float width, float height, float radius, ID2D1Brush brush, float stroke) =>
        drawing.DrawRoundedRectangle(new RoundedRectangle(new RectangleF(x, y, width, height), radius, radius), brush, stroke);

    private void DrawText(ID2D1DeviceContext drawing, string value, float x, float y, float width, float height, float size, ID2D1Brush brush, TextAlignment alignment = TextAlignment.Leading)
    {
        var format = GetTextFormat(size);
        format.TextAlignment = alignment;
        drawing.DrawText(value, format, new Rect(x, y, width, height), brush);
    }

    private IDWriteTextFormat GetTextFormat(float size)
    {
        if (_textFormats.TryGetValue(size, out var existing))
        {
            return existing;
        }

        var format = _writeFactory.CreateTextFormat(
            "Bahnschrift",
            null,
            FontWeight.SemiBold,
            FontStyle.Normal,
            FontStretch.Normal,
            size,
            "pt-BR");
        format.ParagraphAlignment = ParagraphAlignment.Center;
        _textFormats.Add(size, format);
        return format;
    }

    private static string FormatLap(double seconds) =>
        seconds > 0 && double.IsFinite(seconds)
            ? TimeSpan.FromSeconds(seconds).ToString(@"m\:ss\.fff")
            : "--:--.---";

    private static Color4 Color(byte red, byte green, byte blue, byte alpha = 255) =>
        new(red / 255f, green / 255f, blue / 255f, alpha / 255f);

    private readonly record struct PedalSample(long Timestamp, float Throttle, float Brake);

    private void ReleaseDrawingResources()
    {
        _white?.Dispose(); _white = null;
        _muted?.Dispose(); _muted = null;
        _green?.Dispose(); _green = null;
        _cyan?.Dispose(); _cyan = null;
        _amber?.Dispose(); _amber = null;
        _red?.Dispose(); _red = null;
        _blue?.Dispose(); _blue = null;
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
