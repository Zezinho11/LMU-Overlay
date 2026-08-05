using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using LmuOverlay.Widgets;

namespace LmuOverlay.SteamVr;

public static class VrDashboardTexture
{
    public const int Width = 1024;
    public const int Height = 512;

    public static byte[] Render(DashboardWidgetState dashboard)
    {
        using var bitmap = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.FromArgb(235, 5, 8, 12));
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        using var red = new SolidBrush(Color.FromArgb(255, 236, 31, 45));
        using var cyan = new SolidBrush(Color.FromArgb(255, 41, 211, 203));
        using var white = new SolidBrush(Color.White);
        using var muted = new SolidBrush(Color.FromArgb(255, 145, 158, 174));
        using var dark = new SolidBrush(Color.FromArgb(255, 18, 24, 31));
        using var border = new Pen(Color.FromArgb(255, 70, 82, 96), 3);
        using var headerFont = new Font("Segoe UI", 42, FontStyle.Bold, GraphicsUnit.Pixel);
        using var labelFont = new Font("Segoe UI", 24, FontStyle.Bold, GraphicsUnit.Pixel);
        using var valueFont = new Font("Segoe UI", 92, FontStyle.Bold, GraphicsUnit.Pixel);
        using var gearFont = new Font("Segoe UI", 190, FontStyle.Bold, GraphicsUnit.Pixel);

        graphics.FillRectangle(red, 0, 0, Width, 12);
        graphics.DrawString("REDFOX RACING", headerFont, white, 42, 28);
        graphics.DrawString("STEAMVR PREVIEW", labelFont, muted, 750, 43);

        var connected = dashboard.Available;
        graphics.FillRectangle(connected ? cyan : red, 42, 105, 230, 44);
        graphics.DrawString(connected ? "LMU CONNECTED" : "WAITING FOR LMU", labelFont, dark, 51, 112);

        graphics.FillRectangle(dark, 42, 174, 430, 270);
        graphics.DrawRectangle(border, 42, 174, 430, 270);
        graphics.DrawString("SPEED", labelFont, muted, 70, 198);
        graphics.DrawString(
            connected ? $"{dashboard.SpeedKilometersPerHour:0}" : "---",
            valueFont,
            white,
            66,
            245);
        graphics.DrawString("KM/H", labelFont, muted, 310, 356);

        graphics.FillRectangle(dark, 492, 174, 490, 270);
        graphics.DrawRectangle(border, 492, 174, 490, 270);
        graphics.DrawString("GEAR", labelFont, muted, 520, 198);
        var gear = connected ? dashboard.Gear : "-";
        graphics.DrawString(gear, gearFont, white, 620, 205);

        var rpmFraction = connected ? dashboard.EngineRpmFraction : 0;
        using var rpmBackground = new SolidBrush(Color.FromArgb(255, 34, 43, 53));
        graphics.FillRectangle(rpmBackground, 42, 466, 940, 22);
        graphics.FillRectangle(
            rpmFraction >= 0.9 ? red : cyan,
            42,
            466,
            (int)Math.Round(940 * rpmFraction),
            22);

        return ToRgba(bitmap);
    }

    internal static byte[] ToRgba(Bitmap bitmap)
    {
        var rectangle = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var rgba = new byte[checked(bitmap.Width * bitmap.Height * 4)];
            var rowBytes = bitmap.Width * 4;
            if (data.Stride == rowBytes)
            {
                Marshal.Copy(data.Scan0, rgba, 0, rgba.Length);
            }
            else
            {
                for (var y = 0; y < bitmap.Height; y++)
                {
                    var sourceRow = data.Stride >= 0 ? y : bitmap.Height - 1 - y;
                    Marshal.Copy(
                        data.Scan0 + sourceRow * Math.Abs(data.Stride),
                        rgba,
                        y * rowBytes,
                        rowBytes);
                }
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
}
