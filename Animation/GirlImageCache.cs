using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskLofi.Services;

namespace DeskLofi.Animation;

/// <summary>Loads Girl animation images once and retains frozen sources for the window lifetime.</summary>
public sealed class GirlImageCache
{
    public BitmapSource Master { get; }
    public BitmapSource BlinkHalf { get; }
    public BitmapSource BlinkClose { get; }
    public BitmapSource[] TypingFrames { get; }
    public BitmapSource[] MouseFrames { get; }
    public BitmapSource[] CoffeeFrames { get; }
    public BitmapSource[] StretchFrames { get; }
    public bool HasBlink { get; }
    public bool ReconstructedCloseAlpha { get; }

    public GirlImageCache(AssetPackService packs, Func<BitmapSource> fallback)
    {
        Master = packs.GetCharacterAnimation("idle").FirstOrDefault() ?? fallback();
        var blinkFrames = packs.GetCharacterAnimation("blink");
        HasBlink = blinkFrames.Length >= 2;
        BlinkHalf = HasBlink ? blinkFrames[0] : Master;
        var close = HasBlink ? blinkFrames[^1] : Master;
        if (HasBlink && close.PixelWidth == Master.PixelWidth && close.PixelHeight == Master.PixelHeight)
        {
            BlinkClose = ApplyMasterAlpha(close, Master);
            ReconstructedCloseAlpha = true;
            LoggerService.Info("Character blink alpha reconstructed from idle silhouette");
        }
        else BlinkClose = close;
        TypingFrames = packs.GetCharacterAnimation("typing");
        var mouseFrames = packs.GetCharacterAnimation("mouse");
        MouseFrames = mouseFrames.Select((frame, index) => index == 0 ? frame : RemoveCheckerboardBackground(frame, mouseFrames[0], $"mouse[{index}]")).ToArray();
        var coffeeFrames = packs.GetCharacterAnimation("coffee");
        CoffeeFrames = coffeeFrames.Select((frame, index) => index == 0 ? frame : RemoveCheckerboardBackground(frame, coffeeFrames[0], $"coffee[{index}]")).ToArray();
        var stretchFrames = packs.GetCharacterAnimation("stretch");
        StretchFrames = stretchFrames.Select((frame, index) => index == stretchFrames.Length - 1 ? frame : RemoveCheckerboardBackground(frame, stretchFrames[^1], $"stretch[{index}]", Master, clearCanvasEdge: true)).ToArray();
        if (HasBlink) { LogSizeMismatch("blink[0]", BlinkHalf, Master); LogSizeMismatch("blink[last]", BlinkClose, Master); }
        for (var i = 0; i < TypingFrames.Length; i++) LogSizeMismatch($"typing[{i}]", TypingFrames[i], Master);
        for (var i = 0; i < MouseFrames.Length; i++) LogSizeMismatch($"mouse[{i}]", MouseFrames[i], Master);
        for (var i = 0; i < CoffeeFrames.Length; i++) LogSizeMismatch($"coffee[{i}]", CoffeeFrames[i], Master);
        for (var i = 0; i < StretchFrames.Length; i++) LogSizeMismatch($"stretch[{i}]", StretchFrames[i], Master);
    }

    private static BitmapSource ApplyMasterAlpha(BitmapSource closed, BitmapSource master)
    {
        var closedBgra = new FormatConvertedBitmap(closed, PixelFormats.Bgra32, null, 0);
        var masterBgra = new FormatConvertedBitmap(master, PixelFormats.Bgra32, null, 0);
        var stride = closed.PixelWidth * 4;
        var pixels = new byte[stride * closed.PixelHeight];
        var alphaMask = new byte[stride * master.PixelHeight];
        closedBgra.CopyPixels(pixels, stride, 0); masterBgra.CopyPixels(alphaMask, stride, 0);
        for (var offset = 3; offset < pixels.Length; offset += 4) pixels[offset] = (byte)(pixels[offset] * alphaMask[offset] / 255);
        var result = BitmapSource.Create(closed.PixelWidth, closed.PixelHeight, closed.DpiX, closed.DpiY, PixelFormats.Bgra32, null, pixels, stride);
        result.Freeze(); return result;
    }

    // Some supplied frames contain an opaque gray checkerboard. Remove only light,
    // neutral-colored regions outside a transparent reference frame's silhouette,
    // plus the canvas edge. A second reference can uncover gaps hidden by a pose.
    // The original PNGs remain intact.
    internal static BitmapSource RemoveCheckerboardBackground(BitmapSource source, BitmapSource silhouette, string name,
        BitmapSource? secondarySilhouette = null, bool clearCanvasEdge = false)
    {
        var bgra = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var width = bgra.PixelWidth;
        var height = bgra.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[stride * height];
        bgra.CopyPixels(pixels, stride, 0);
        if (pixels[3] == 0) return source;
        if (silhouette.PixelWidth != width || silhouette.PixelHeight != height) return source;
        var reference = new FormatConvertedBitmap(silhouette, PixelFormats.Bgra32, null, 0);
        var referencePixels = new byte[pixels.Length];
        reference.CopyPixels(referencePixels, stride, 0);
        byte[]? secondaryPixels = null;
        if (secondarySilhouette?.PixelWidth == width && secondarySilhouette.PixelHeight == height)
        {
            var secondary = new FormatConvertedBitmap(secondarySilhouette, PixelFormats.Bgra32, null, 0);
            secondaryPixels = new byte[pixels.Length];
            secondary.CopyPixels(secondaryPixels, stride, 0);
        }

        var queued = new byte[width * height];
        var queue = new int[width * height];
        var head = 0;
        var tail = 0;
        void Add(int index)
        {
            if (queued[index] != 0) return;
            var offset = index * 4;
            var blue = pixels[offset];
            var green = pixels[offset + 1];
            var red = pixels[offset + 2];
            var darkest = Math.Min(red, Math.Min(green, blue));
            var lightest = Math.Max(red, Math.Max(green, blue));
            if (pixels[offset + 3] == 0 || darkest < 140 || lightest - darkest > 16) return;
            queued[index] = 1;
            queue[tail++] = index;
        }

        for (var x = 0; x < width; x++) { Add(x); Add((height - 1) * width + x); }
        for (var y = 0; y < height; y++) { Add(y * width); Add(y * width + width - 1); }
        for (var index = 0; index < queued.Length; index++)
            if (referencePixels[index * 4 + 3] == 0 || secondaryPixels?[index * 4 + 3] == 0) Add(index);
        while (head < tail)
        {
            var index = queue[head++];
            pixels[index * 4 + 3] = 0;
            var x = index % width;
            if (x > 0) Add(index - 1);
            if (x + 1 < width) Add(index + 1);
            if (index >= width) Add(index - width);
            if (index < width * (height - 1)) Add(index + width);
        }
        if (clearCanvasEdge)
        {
            const int margin = 5;
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                if (x < margin || y < margin || x >= width - margin || y >= height - margin)
                    pixels[(y * width + x) * 4 + 3] = 0;
        }
        if (tail == 0) return source;
        var result = BitmapSource.Create(width, height, source.DpiX, source.DpiY, PixelFormats.Bgra32, null, pixels, stride);
        result.Freeze();
        LoggerService.Info($"Removed opaque checkerboard from {name} ({tail} background pixels).");
        return result;
    }

    private static void LogSizeMismatch(string name, BitmapSource image, BitmapSource master)
    {
        if (image.PixelWidth != master.PixelWidth || image.PixelHeight != master.PixelHeight)
            LoggerService.Error($"Girl image canvas mismatch: {name} {image.PixelWidth}x{image.PixelHeight}, MASTER {master.PixelWidth}x{master.PixelHeight}");
    }
}
