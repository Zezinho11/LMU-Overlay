using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace LmuOverlay.SteamVr;

internal sealed class VrCanvas : IDisposable
{
    private readonly Bitmap _bitmap;
    private readonly Graphics _graphics;
    private readonly Dictionary<(float Size, bool Bold), Font> _fonts = [];

    public VrCanvas(int width, int height, VrRenderStyle style)
    {
        Width = width;
        Height = height;
        Style = style;
        _bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        _graphics = Graphics.FromImage(_bitmap);
        _graphics.Clear(Color.Transparent);
        _graphics.SmoothingMode = SmoothingMode.AntiAlias;
        _graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        _graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
    }

    public int Width { get; }
    public int Height { get; }
    public VrRenderStyle Style { get; }
    public Graphics Graphics => _graphics;

    public IDisposable Translate(float x, float y)
    {
        var state = _graphics.Save();
        _graphics.TranslateTransform(x, y);
        return new GraphicsScope(_graphics, state);
    }

    public void Fill(Color color, float x, float y, float width, float height)
    {
        using var brush = new SolidBrush(color);
        _graphics.FillRectangle(brush, x, y, width, height);
    }

    public void FillRound(Color color, float x, float y, float width, float height, float radius)
    {
        using var path = RoundPath(x, y, width, height, radius);
        using var brush = new SolidBrush(color);
        _graphics.FillPath(brush, path);
    }

    public void StrokeRound(Color color, float thickness, float x, float y, float width, float height, float radius)
    {
        using var path = RoundPath(x, y, width, height, radius);
        using var pen = new Pen(color, thickness);
        _graphics.DrawPath(pen, path);
    }

    public void Text(
        string? value,
        float size,
        Color color,
        RectangleF bounds,
        bool bold = false,
        StringAlignment alignment = StringAlignment.Near)
    {
        using var format = new StringFormat
        {
            Alignment = alignment,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap,
        };
        using var brush = new SolidBrush(color);
        _graphics.DrawString(value ?? string.Empty, Font(size, bold), brush, bounds, format);
    }

    public void Line(Color color, float thickness, PointF first, PointF second)
    {
        using var pen = new Pen(color, thickness)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        };
        _graphics.DrawLine(pen, first, second);
    }

    public byte[] Pixels() => ToRgba(_bitmap);

    private Font Font(float size, bool bold)
    {
        var key = (Math.Max(6, size), bold);
        if (_fonts.TryGetValue(key, out var font)) return font;
        font = new Font(
            "Bahnschrift",
            key.Item1,
            bold ? FontStyle.Bold : FontStyle.Regular,
            GraphicsUnit.Pixel);
        _fonts.Add(key, font);
        return font;
    }

    private static GraphicsPath RoundPath(float x, float y, float width, float height, float radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(Math.Min(width, height), radius * 2);
        path.AddArc(x, y, diameter, diameter, 180, 90);
        path.AddArc(x + width - diameter, y, diameter, diameter, 270, 90);
        path.AddArc(x + width - diameter, y + height - diameter, diameter, diameter, 0, 90);
        path.AddArc(x, y + height - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static byte[] ToRgba(Bitmap bitmap)
    {
        var rectangle = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var rgba = new byte[checked(bitmap.Width * bitmap.Height * 4)];
            var rowBytes = bitmap.Width * 4;
            for (var y = 0; y < bitmap.Height; y++)
            {
                var sourceRow = data.Stride >= 0 ? y : bitmap.Height - 1 - y;
                Marshal.Copy(
                    data.Scan0 + sourceRow * Math.Abs(data.Stride),
                    rgba,
                    y * rowBytes,
                    rowBytes);
            }
            for (var index = 0; index < rgba.Length; index += 4)
            {
                (rgba[index], rgba[index + 2]) = (rgba[index + 2], rgba[index]);
            }
            return rgba;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    public void Dispose()
    {
        foreach (var font in _fonts.Values) font.Dispose();
        _fonts.Clear();
        _graphics.Dispose();
        _bitmap.Dispose();
    }

    private sealed class GraphicsScope(Graphics graphics, GraphicsState state) : IDisposable
    {
        public void Dispose() => graphics.Restore(state);
    }
}

public readonly record struct VrPedalSample(float Throttle, float Brake);
