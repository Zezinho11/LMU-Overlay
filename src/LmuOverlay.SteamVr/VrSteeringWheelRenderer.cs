using System.Drawing;
using System.Drawing.Drawing2D;
using LmuOverlay.Widgets;

namespace LmuOverlay.SteamVr;

internal static class VrSteeringWheelRenderer
{
    private const string Resource = "LmuOverlay.SteamVr.Assets.steering-wheel.png";
    private static readonly Lazy<Bitmap?> DefaultImage = new(LoadDefault);
    private static readonly object Sync = new();
    private static string _customPath = string.Empty;
    private static Bitmap? _customImage;

    internal static void Draw(VrCanvas canvas, double steering, double rangeDegrees, string customPath)
    {
        var graphics = canvas.Graphics;
        using (var backing = new SolidBrush(canvas.Style.SecondaryText))
        using (var outline = new Pen(canvas.Style.Information, 2.5f))
        {
            graphics.FillEllipse(backing, 40, 114, 196, 196);
            graphics.DrawEllipse(outline, 39, 113, 198, 198);
        }
        var state = graphics.Save();
        graphics.TranslateTransform(138, 212);
        graphics.RotateTransform((float)SteeringWheelRotation.AngleDegrees(steering, rangeDegrees));
        if (Resolve(customPath) is { } image)
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.DrawImage(image, new RectangleF(-100, -100, 200, 200));
            graphics.Restore(state);
            return;
        }

        using var rim = new Pen(canvas.Style.PrimaryText, 18) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var spoke = new Pen(canvas.Style.SecondaryText, 12) { StartCap = LineCap.Round };
        graphics.DrawEllipse(rim, -82, -82, 164, 164);
        graphics.DrawLine(spoke, 0, 0, -68, -30);
        graphics.DrawLine(spoke, 0, 0, 68, -30);
        graphics.DrawLine(spoke, 0, 0, 0, 72);
        using var hub = new SolidBrush(canvas.Style.Accent);
        graphics.FillEllipse(hub, -24, -24, 48, 48);
        graphics.Restore(state);
    }

    private static Bitmap? Resolve(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return DefaultImage.Value;
        lock (Sync)
        {
            if (string.Equals(_customPath, path, StringComparison.OrdinalIgnoreCase))
                return _customImage ?? DefaultImage.Value;
            _customImage?.Dispose();
            _customImage = null;
            _customPath = path;
            try
            {
                if (File.Exists(path))
                {
                    using var source = new Bitmap(path);
                    _customImage = new Bitmap(source);
                }
            }
            catch (Exception exception) when (exception is ArgumentException or IOException or OutOfMemoryException)
            {
                _customImage = null;
            }
            return _customImage ?? DefaultImage.Value;
        }
    }

    private static Bitmap? LoadDefault()
    {
        try
        {
            using var stream = typeof(VrWidgetTextureRenderer).Assembly.GetManifestResourceStream(Resource);
            if (stream is null) return null;
            using var source = new Bitmap(stream);
            return new Bitmap(source);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or OutOfMemoryException)
        {
            return null;
        }
    }
}
