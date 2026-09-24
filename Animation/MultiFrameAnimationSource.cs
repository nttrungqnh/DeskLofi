using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskLofi.Models;

namespace DeskLofi.Animation;

/// <summary>Decodes separate PNG frames once and places them on one fixed, transparent canvas.</summary>
public static class MultiFrameAnimationSource
{
    public static BitmapSource[] Load(IEnumerable<string> paths, SpriteAnimationDefinition definition, int canvasWidth, int canvasHeight)
    {
        return paths.Select(path => PlaceOnCanvas(ReadPng(path), definition, canvasWidth, canvasHeight)).ToArray();
    }

    public static BitmapSource ReadPng(string file)
    {
        if (!File.Exists(file)) throw new FileNotFoundException("Animation PNG missing.", file);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(file);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    public static BitmapSource PlaceOnCanvas(BitmapSource source, SpriteAnimationDefinition definition, int canvasWidth, int canvasHeight)
    {
        var scale = definition.SourceScale;
        if (!double.IsFinite(scale) || scale <= 0)
            throw new InvalidDataException($"Animation scale {scale} must be finite and positive.");
        var width = checked((int)Math.Round(source.PixelWidth * scale, MidpointRounding.AwayFromZero));
        var height = checked((int)Math.Round(source.PixelHeight * scale, MidpointRounding.AwayFromZero));
        if (width < 1 || height < 1 || width > canvasWidth || height > canvasHeight)
            throw new InvalidDataException($"Scaled frame {width}x{height} exceeds canvas {canvasWidth}x{canvasHeight}.");

        var left = definition.HorizontalAnchor switch
        {
            FrameHorizontalAnchor.Left => 0,
            FrameHorizontalAnchor.Right => canvasWidth - width,
            _ => (canvasWidth - width) / 2
        } + definition.OffsetX;
        var top = definition.VerticalAnchor switch
        {
            FrameVerticalAnchor.Top => 0,
            FrameVerticalAnchor.Center => (canvasHeight - height) / 2,
            _ => canvasHeight - height
        } + definition.OffsetY;
        if (left < 0 || top < 0 || left + width > canvasWidth || top + height > canvasHeight)
            throw new InvalidDataException($"Frame placement {left},{top} lies outside canvas {canvasWidth}x{canvasHeight}.");

        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var rowBytes = source.PixelWidth * 4;
        var pixels = new byte[rowBytes * source.PixelHeight];
        converted.CopyPixels(pixels, rowBytes, 0);
        var canvasStride = canvasWidth * 4;
        var canvas = new byte[canvasStride * canvasHeight];
        if (scale == 1)
        {
            for (var y = 0; y < height; y++)
                Buffer.BlockCopy(pixels, y * rowBytes, canvas, (top + y) * canvasStride + left * 4, rowBytes);
        }
        else
        {
            for (var y = 0; y < height; y++)
            {
                var sourceRow = Math.Min(source.PixelHeight - 1, (int)(y / scale)) * rowBytes;
                var destinationRow = (top + y) * canvasStride + left * 4;
                for (var x = 0; x < width; x++)
                {
                    var sourceOffset = sourceRow + Math.Min(source.PixelWidth - 1, (int)(x / scale)) * 4;
                    Buffer.BlockCopy(pixels, sourceOffset, canvas, destinationRow + x * 4, 4);
                }
            }
        }
        var result = BitmapSource.Create(canvasWidth, canvasHeight, 96, 96, PixelFormats.Bgra32, null, canvas, canvasStride);
        result.Freeze();
        return result;
    }
}
