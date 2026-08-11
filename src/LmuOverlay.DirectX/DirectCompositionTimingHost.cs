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

internal sealed class DirectCompositionTimingHost : IDisposable
{
    private readonly ID3D11Device _d3dDevice;
    private readonly ID3D11DeviceContext _d3dContext;
    private readonly IDXGIDevice _dxgiDevice;
    private readonly IDXGIFactory2 _dxgiFactory;
    private readonly IDCompositionDevice _compositionDevice;
    private readonly IDWriteFactory _writeFactory;
    private readonly Dictionary<float, IDWriteTextFormat> _formats = [];
    private readonly TimingSurface _standings;
    private readonly TimingSurface _relative;
    private long _renderedSequence = -1;
    private float _textScale = 1;
    private string _language = OverlayText.PortugueseBrazil;

    public DirectCompositionTimingHost()
    {
        D3D11CreateDevice(
            IntPtr.Zero,
            DriverType.Hardware,
            DeviceCreationFlags.BgraSupport,
            [
                Vortice.Direct3D.FeatureLevel.Level_11_1,
                Vortice.Direct3D.FeatureLevel.Level_11_0,
                Vortice.Direct3D.FeatureLevel.Level_10_1,
                Vortice.Direct3D.FeatureLevel.Level_10_0,
            ],
            out _d3dDevice,
            out _d3dContext).CheckError();
        _dxgiDevice = _d3dDevice.QueryInterface<IDXGIDevice>();
        _dxgiFactory = CreateDXGIFactory2<IDXGIFactory2>(false);
        _compositionDevice = DCompositionCreateDevice<IDCompositionDevice>(_dxgiDevice);
        _writeFactory = DWriteCreateFactory<IDWriteFactory>();
        _standings = new TimingSurface(this, "Live Standings");
        _relative = new TimingSurface(this, "Relative");
    }

    public void Render(NativeTimingFrame frame)
    {
        if (frame.Sequence == _renderedSequence)
        {
            return;
        }

        _textScale = (float)Math.Clamp(
            (frame.Style ?? NativeOverlayStyle.RedFox).TimingTextScale,
            0.8,
            1.25);
        _language = (frame.Style ?? NativeOverlayStyle.RedFox).Language;

        _standings.Render(
            frame.LiveStandingsBounds,
            frame.LiveStandingsVisible,
            frame.LiveStandingsOpacity,
            frame.Style ?? NativeOverlayStyle.RedFox,
            drawing => DrawStandings(drawing, frame.LiveStandings));
        _relative.Render(
            frame.RelativeBounds,
            frame.RelativeVisible,
            frame.RelativeOpacity,
            frame.Style ?? NativeOverlayStyle.RedFox,
            drawing => DrawRelative(drawing, frame.Relative));
        _renderedSequence = frame.Sequence;
    }

    public void PumpMessages() => TimingSurface.PumpMessages();

    private void DrawStandings(TimingSurface surface, LiveStandingsWidgetState state)
    {
        var drawing = surface.Drawing;
        Fill(drawing, 0, 0, 500, 46, surface.Header);
        DrawText(drawing, OverlayText.TranslateExact(_language, state.SessionName), 14, 4, 270, 38, 18, surface.White);
        DrawText(drawing, FormatSessionClock(state.SessionRemainingSeconds), 330, 4, 154, 38, 20, surface.White, TextAlignment.Trailing);
        Fill(drawing, 0, 46, 500, 22, surface.ColumnHeader);
        DrawText(drawing, "P", 0, 47, 36, 20, 8, surface.Muted, TextAlignment.Center);
        DrawText(drawing, OverlayText.Get(_language, OverlayTextKey.Manufacturer), 36, 47, 50, 20, 8, surface.Muted, TextAlignment.Center);
        DrawText(drawing, OverlayText.Get(_language, OverlayTextKey.Number), 86, 47, 40, 20, 8, surface.Muted, TextAlignment.Center);
        DrawText(drawing, OverlayText.Get(_language, OverlayTextKey.Driver), 126, 47, 62, 20, 8, surface.Muted, TextAlignment.Center);
        DrawText(drawing, OverlayText.Get(_language, state.IsQualifying ? OverlayTextKey.Best : OverlayTextKey.LastLap), 188, 47, 104, 20, 8, surface.Muted, TextAlignment.Center);
        DrawText(drawing, OverlayText.Get(_language, OverlayTextKey.Gap), 292, 47, 76, 20, 8, surface.Muted, TextAlignment.Center);
        DrawText(drawing, "TYRE / NRG", 368, 47, 132, 20, 8, surface.Muted, TextAlignment.Center);

        var rowCount = state.Classes.Sum(category => category.Rows.Count);
        const float rowHeight = 25f;
        var y = 68f;
        var alternating = 0;
        foreach (var category in state.Classes)
        {
            Fill(drawing, 0, y, 500, 18, category.IsPlayerClass ? surface.ClassRed : surface.ClassBlue);
            DrawText(drawing, category.ClassName.ToUpperInvariant(), 12, y, 476, 18, 10, surface.White);
            y += 18;
            foreach (var row in category.Rows)
            {
                Fill(
                    drawing,
                    0,
                    y,
                    500,
                    rowHeight,
                    row.IsPlayer ? surface.Player : alternating++ % 2 == 0 ? surface.RowOne : surface.RowTwo);
                DrawText(drawing, row.ClassPosition.ToString(), 0, y, 36, rowHeight, 11,
                    row.ClassPosition == 1 ? surface.Gold : surface.White, TextAlignment.Center);
                var (brand, brandBrush) = Manufacturer(surface, row.VehicleModel);
                FillRounded(drawing, 40, y + 3, 42, Math.Max(12, rowHeight - 6), 2, brandBrush);
                DrawText(drawing, brand, 40, y + 2, 42, Math.Max(13, rowHeight - 4), 9, surface.White, TextAlignment.Center);
                DrawText(drawing, row.CarNumber, 86, y, 40, rowHeight, 9, surface.White, TextAlignment.Center);
                DrawText(drawing, row.DriverAbbreviation, 126, y, 62, rowHeight, 10, surface.White, TextAlignment.Center);
                DrawText(drawing, FormatLap(row.LastLapTimeSeconds), 188, y, 104, rowHeight, 9, surface.White, TextAlignment.Center);
                DrawText(drawing, FormatInterval(row), 292, y, 76, rowHeight, 9,
                    row.IsInPitLane && !row.IsQualifying ? surface.Amber : surface.White, TextAlignment.Center);
                DrawTireEnergy(drawing, surface, row, y, rowHeight);
                y += rowHeight;
            }
        }

        if (rowCount == 0)
        {
            DrawText(drawing, OverlayText.Get(_language, OverlayTextKey.Waiting), 30, 190, 440, 30, 13, surface.Muted, TextAlignment.Center);
        }
    }

    private void DrawRelative(TimingSurface surface, RelativeWidgetState state)
    {
        var drawing = surface.Drawing;
        Fill(drawing, 0, 0, 500, 24, surface.ColumnHeader);
        DrawText(drawing, OverlayText.Get(_language, OverlayTextKey.Relative).ToUpperInvariant(), 14, 0, 250, 24, 10, surface.Muted);
        DrawText(drawing, OverlayText.Get(_language, OverlayTextKey.Gap), 400, 0, 84, 24, 10, surface.Muted, TextAlignment.Trailing);
        if (state.Rows.Count == 0)
        {
            DrawText(drawing, OverlayText.Get(_language, OverlayTextKey.Waiting), 30, 190, 440, 30, 13, surface.Muted, TextAlignment.Center);
            return;
        }

        var rowHeight = Math.Min(37f, 386f / Math.Max(1, state.Rows.Count));
        var y = 24f;
        for (var index = 0; index < state.Rows.Count; index++)
        {
            var row = state.Rows[index];
            var foreground = row.IsPlayer ? surface.DarkText : surface.White;
            Fill(drawing, 0, y, 500, rowHeight,
                row.IsPlayer ? surface.PlayerLight : index % 2 == 0 ? surface.RelativeOne : surface.RelativeTwo);
            var classBrush = ClassBrush(surface, row.ClassAbbreviation);
            FillRounded(drawing, 12, y + 6, 48, rowHeight - 12, 3, classBrush);
            FillRounded(drawing, 60, y + 6, 58, rowHeight - 12, 3, surface.White);
            DrawText(drawing, row.OverallPosition.ToString(), 12, y + 5, 48, rowHeight - 10, 13, surface.White, TextAlignment.Center);
            DrawText(drawing, row.ClassAbbreviation, 60, y + 5, 58, rowHeight - 10, 11, classBrush, TextAlignment.Center);
            DrawText(drawing, row.DriverDisplayName, 130, y, 228, rowHeight, 14, foreground);
            DrawText(drawing, FormatRelative(row), 368, y, 116, rowHeight, 14,
                row.IsInPitLane && !row.IsPlayer ? surface.Amber : foreground, TextAlignment.Trailing);
            y += rowHeight;
        }
    }

    private IDWriteTextFormat GetTextFormat(float size, TextAlignment alignment)
    {
        size *= _textScale;
        if (!_formats.TryGetValue(size, out var format))
        {
            format = _writeFactory.CreateTextFormat(
                "Bahnschrift", null, FontWeight.SemiBold, FontStyle.Normal,
                FontStretch.Normal, size, "pt-BR");
            format.ParagraphAlignment = ParagraphAlignment.Center;
            _formats.Add(size, format);
        }
        format.TextAlignment = alignment;
        return format;
    }

    private void DrawText(ID2D1DeviceContext drawing, string text, float x, float y,
        float width, float height, float size, ID2D1Brush brush,
        TextAlignment alignment = TextAlignment.Leading) =>
        drawing.DrawText(text, GetTextFormat(size, alignment), new Rect(x, y, width, height), brush);

    private static void Fill(ID2D1DeviceContext drawing, float x, float y, float width, float height, ID2D1Brush brush) =>
        drawing.FillRectangle(new RectangleF(x, y, width, height), brush);

    private static void FillRounded(ID2D1DeviceContext drawing, float x, float y, float width, float height, float radius, ID2D1Brush brush) =>
        drawing.FillRoundedRectangle(new RoundedRectangle(new RectangleF(x, y, width, height), radius, radius), brush);

    private static string FormatLap(double seconds) => seconds > 0 && double.IsFinite(seconds)
        ? TimeSpan.FromSeconds(seconds).ToString(@"m\:ss\.fff")
        : "--:--.---";

    private static string FormatInterval(LiveStandingsRowState row) =>
        row.IsQualifying ?
            row.ClassPosition == 1 ? "LEADER" :
            row.IntervalSeconds >= 0 && double.IsFinite(row.IntervalSeconds) ? $"+{row.IntervalSeconds:0.000}" : "--.---" :
        row.IsInPitLane ? "PIT" :
        row.ClassPosition == 1 ? "LEADER" :
        row.IntervalLaps > 0 ? $"+{row.IntervalLaps} L" :
        row.IntervalSeconds > 0 && double.IsFinite(row.IntervalSeconds) ? $"+{row.IntervalSeconds:0.000}" : "--.---";

    private void DrawTireEnergy(
        ID2D1DeviceContext drawing,
        TimingSurface surface,
        LiveStandingsRowState row,
        float y,
        float rowHeight)
    {
        if (row.IsInPitLane)
        {
            DrawText(drawing, "PIT", 376, y, 116, rowHeight, 10, surface.Amber, TextAlignment.Center);
            return;
        }

        var code = TireCompoundCode(row.TireCompound);
        var compoundBrush = TireCompoundBrush(surface, code);
        FillRounded(drawing, 377, y + 3, 24, rowHeight - 6, 9, compoundBrush);
        DrawText(drawing, code, 377, y + 2, 24, rowHeight - 4, 9,
            code is "M" or "H" ? surface.DarkText : surface.White,
            TextAlignment.Center);
        var hasEnergy = row.VirtualEnergyFraction is >= 0 and <= 1 &&
                        double.IsFinite(row.VirtualEnergyFraction);
        Fill(drawing, 407, y + 5, 4, rowHeight - 10,
            !hasEnergy ? surface.BrandGray :
            row.VirtualEnergyFraction <= 0.10 ? surface.BrandRed :
            row.VirtualEnergyFraction <= 0.25 ? surface.Amber : surface.ClassGreen);
        DrawText(drawing, hasEnergy ? $"{row.VirtualEnergyFraction:P0}" : "--%", 417, y, 75, rowHeight, 10,
            surface.White, TextAlignment.Leading);
    }

    private static string TireCompoundCode(string compound)
    {
        var value = compound.Trim().ToUpperInvariant();
        return value switch
        {
            _ when value.Contains("SOFT", StringComparison.Ordinal) => "S",
            _ when value.Contains("MED", StringComparison.Ordinal) => "M",
            _ when value.Contains("HARD", StringComparison.Ordinal) => "H",
            _ when value.Contains("WET", StringComparison.Ordinal) => "W",
            _ when value.Contains("INTER", StringComparison.Ordinal) => "I",
            { Length: > 0 } => value[..Math.Min(2, value.Length)],
            _ => "--",
        };
    }

    private static ID2D1Brush TireCompoundBrush(TimingSurface surface, string code) => code switch
    {
        "S" => surface.BrandRed,
        "M" => surface.Gold,
        "H" => surface.White,
        "W" => surface.BrandBlue,
        "I" => surface.ClassGreen,
        _ => surface.BrandGray,
    };

    private static string FormatSessionClock(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds <= 0)
        {
            return "--:--";
        }

        var value = TimeSpan.FromSeconds(seconds);
        return value.TotalHours >= 1
            ? $"{(int)value.TotalHours}:{value.Minutes:00}:{value.Seconds:00}"
            : $"{value.Minutes}:{value.Seconds:00}";
    }

    private static string FormatRelative(RelativeRowState row) =>
        row.IsInPitLane ? "PIT" : row.IsPlayer ? "0.0" :
        double.IsFinite(row.RelativeGapSeconds) ? row.RelativeGapSeconds.ToString("+0.0;-0.0;0.0") : "--.-";

    private static (string Code, ID2D1Brush Brush) Manufacturer(TimingSurface s, string value)
    {
        var name = value.ToUpperInvariant();
        if (name.Contains("BMW")) return ("BMW", s.BrandBlue);
        if (name.Contains("FERRARI")) return ("FER", s.BrandRed);
        if (name.Contains("PORSCHE")) return ("POR", s.BrandGray);
        if (name.Contains("CADILLAC")) return ("CAD", s.BrandYellow);
        if (name.Contains("ALPINE")) return ("ALP", s.BrandBlue);
        if (name.Contains("FORD")) return ("FOR", s.BrandBlue);
        if (name.Contains("LEXUS")) return ("LEX", s.BrandGray);
        if (name.Contains("ASTON")) return ("AST", s.BrandGreen);
        if (name.Contains("TOYOTA")) return ("TOY", s.BrandRed);
        if (name.Contains("CORVETTE")) return ("COR", s.BrandYellow);
        if (name.Contains("MCLAREN")) return ("MCL", s.BrandOrange);
        if (name.Contains("LAMBORGHINI")) return ("LAM", s.BrandGreen);
        if (name.Contains("PEUGEOT")) return ("PEU", s.BrandBlue);
        return ("---", s.BrandGray);
    }

    private static ID2D1Brush ClassBrush(TimingSurface surface, string className)
    {
        var value = className.ToUpperInvariant();
        return value.Contains("GT") ? surface.ClassGreen :
            value.Contains("HYP") || value.Contains("HY") ? surface.ClassRed :
            surface.BrandBlue;
    }

    public void Dispose()
    {
        _standings.Dispose();
        _relative.Dispose();
        foreach (var format in _formats.Values) format.Dispose();
        _writeFactory.Dispose();
        _compositionDevice.Dispose();
        _dxgiFactory.Dispose();
        _dxgiDevice.Dispose();
        _d3dContext.Dispose();
        _d3dDevice.Dispose();
    }

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
