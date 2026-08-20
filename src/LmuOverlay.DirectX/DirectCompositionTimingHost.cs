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

internal sealed partial class DirectCompositionTimingHost : IDisposable
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
        var identity = VehicleCatalog.Resolve(value);
        var brush = identity.Code switch
        {
            "FER" or "TOY" => s.BrandRed,
            "CAD" or "LAM" or "COR" => s.BrandYellow,
            "AST" => s.BrandGreen,
            "MCL" => s.BrandOrange,
            "BMW" or "ALP" or "FOR" or "PEU" => s.BrandBlue,
            _ => s.BrandGray,
        };
        return (identity.Code, brush);
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

}
