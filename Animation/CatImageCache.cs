using System.Windows.Media.Imaging;
using DeskLofi.Models;
using DeskLofi.Services;

namespace DeskLofi.Animation;

/// <summary>Loads and freezes the Cat sprites once for reuse by the scene renderer.</summary>
public sealed class CatImageCache
{
    public BitmapSource Master { get; }
    public BitmapSource[] TailFrames { get; }
    public BitmapSource[] SleepFrames { get; }

    public CatImageCache(AssetPackService packs)
    {
        Master = packs.GetPetAnimation("idle").FirstOrDefault() ?? CreateTransparent();
        TailFrames = packs.GetPetAnimation("tail");
        var sleepFrames = packs.GetPetAnimation("sleep");
        SleepFrames = sleepFrames.Select((frame, index) => GirlImageCache.RemoveCheckerboardBackground(
            frame, sleepFrames[Math.Max(0, index - 1)], $"sleep[{index}]", sleepFrames[Math.Min(sleepFrames.Length - 1, index + 1)])).ToArray();

        foreach (var (frame, name) in TailFrames.Select((source, index) => (source, $"tail[{index}]"))
                     .Concat(SleepFrames.Select((source, index) => (source, $"sleep[{index}]"))))
        {
            if (frame.PixelWidth != Master.PixelWidth || frame.PixelHeight != Master.PixelHeight)
                LoggerService.Warn($"Pet sprite canvas mismatch: {name} {frame.PixelWidth}x{frame.PixelHeight}, idle {Master.PixelWidth}x{Master.PixelHeight}");
        }
    }

    private static BitmapSource CreateTransparent()
    {
        var image = BitmapSource.Create(1, 1, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, new byte[4], 4);
        image.Freeze();
        return image;
    }
}
