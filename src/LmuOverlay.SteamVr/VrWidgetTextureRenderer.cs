using System.Drawing;
using LmuOverlay.Widgets;

namespace LmuOverlay.SteamVr;

public sealed record VrRenderedFrame(byte[] Pixels, uint Width, uint Height);

public static partial class VrWidgetTextureRenderer
{
    private static VrRenderStyle DefaultStyle => VrRenderStyle.From(new OverlayProfileSettings());




    private static VrRenderedFrame Draw(int width, int height, VrRenderStyle style, Action<VrCanvas> paint)
    {
        using var canvas = new VrCanvas(width, height, style);
        paint(canvas);
        return new(canvas.Pixels(), (uint)width, (uint)height);
    }

}
