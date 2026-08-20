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
    private static ID2D1Bitmap1? LoadSteeringWheel(
        ID2D1DeviceContext drawing,
        string customPath)
    {
        try
        {
            var customCache = string.IsNullOrWhiteSpace(customPath)
                ? string.Empty
                : Path.ChangeExtension(customPath, ".bgra");
            if (File.Exists(customCache))
            {
                var customPixels = File.ReadAllBytes(customCache);
                if (customPixels.Length == SteeringWheelPixels * SteeringWheelPixels * 4)
                {
                    fixed (byte* source = customPixels)
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
            }

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
}
