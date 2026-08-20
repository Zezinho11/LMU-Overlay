using System.IO;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LmuOverlay.Desktop;

internal static class SteeringWheelAssetStore
{
    public const int MinimumPixels = 256;
    public const int MaximumPixels = 2048;
    public const long MaximumBytes = 5 * 1024 * 1024;
    private const int OutputPixels = 512;

    public static string Import(string sourcePath, string? assetDirectory = null)
    {
        var file = new FileInfo(sourcePath);
        if (!file.Exists)
        {
            throw new FileNotFoundException("A imagem escolhida não existe.", sourcePath);
        }
        if (!string.Equals(file.Extension, ".png", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Use uma imagem PNG para preservar a transparência do volante.");
        }
        if (file.Length <= 0 || file.Length > MaximumBytes)
        {
            throw new InvalidDataException("O PNG do volante deve ter no máximo 5 MB.");
        }

        BitmapFrame frame;
        using (var stream = File.OpenRead(sourcePath))
        {
            var decoder = new PngBitmapDecoder(
                stream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            frame = decoder.Frames.FirstOrDefault()
                ?? throw new InvalidDataException("O PNG não contém uma imagem válida.");
        }
        if (frame.PixelWidth is < MinimumPixels or > MaximumPixels ||
            frame.PixelHeight is < MinimumPixels or > MaximumPixels)
        {
            throw new InvalidDataException(
                $"A imagem deve medir entre {MinimumPixels}×{MinimumPixels} e " +
                $"{MaximumPixels}×{MaximumPixels} pixels.");
        }
        var aspect = frame.PixelWidth / (double)frame.PixelHeight;
        if (aspect is < 0.75 or > 1.3333333333)
        {
            throw new InvalidDataException(
                "Use uma imagem aproximadamente quadrada (proporção entre 3:4 e 4:3).");
        }

        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            var scale = Math.Min(
                OutputPixels / (double)frame.PixelWidth,
                OutputPixels / (double)frame.PixelHeight);
            var width = frame.PixelWidth * scale;
            var height = frame.PixelHeight * scale;
            drawing.DrawImage(
                frame,
                new System.Windows.Rect(
                    (OutputPixels - width) / 2,
                    (OutputPixels - height) / 2,
                    width,
                    height));
        }
        var normalized = new RenderTargetBitmap(
            OutputPixels,
            OutputPixels,
            96,
            96,
            PixelFormats.Pbgra32);
        normalized.Render(visual);

        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath)))[..16];
        var directory = assetDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LMU Overlay",
            "assets");
        Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, $"steering-wheel-{hash}.png");
        if (!File.Exists(destination))
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(normalized));
            using var output = File.Create(destination);
            encoder.Save(output);
        }
        var pixels = new byte[OutputPixels * OutputPixels * 4];
        normalized.CopyPixels(pixels, OutputPixels * 4, 0);
        File.WriteAllBytes(Path.ChangeExtension(destination, ".bgra"), pixels);
        return destination;
    }
}
