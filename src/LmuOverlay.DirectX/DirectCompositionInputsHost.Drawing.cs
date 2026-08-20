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

internal sealed unsafe partial class DirectCompositionInputsHost
{
    private static void FillRounded(ID2D1DeviceContext drawing, float x, float y, float width, float height,
        float radius, ID2D1Brush brush) => drawing.FillRoundedRectangle(
        new RoundedRectangle(new RectangleF(x, y, width, height), radius, radius), brush);

    private static void DrawRounded(ID2D1DeviceContext drawing, float x, float y, float width, float height,
        float radius, ID2D1Brush brush, float stroke) => drawing.DrawRoundedRectangle(
        new RoundedRectangle(new RectangleF(x, y, width, height), radius, radius), brush, stroke);

    private void DrawText(ID2D1DeviceContext drawing, string value, float x, float y, float width, float height,
        float size, ID2D1Brush brush, TextAlignment alignment = TextAlignment.Leading)
    {
        var format = GetTextFormat(size * _textScale);
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
        bool AbsActive,
        bool TcActive);
}
