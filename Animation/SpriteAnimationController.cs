using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using DeskLofi.Models;
using DeskLofi.Services;

namespace DeskLofi.Animation;

/// <summary>Reusable, cached sprite player. It never reads files while advancing a frame.</summary>
public sealed class SpriteAnimationController
{
    private readonly Dictionary<string, SpriteAnimationDefinition> _definitions;
    private readonly Dictionary<string, BitmapSource[]> _framesByAsset = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, BitmapSource[]> _framesByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _fallbackNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<string, BitmapSource[]> _fallback;
    private readonly string _characterName;
    private readonly int _canvasWidth, _canvasHeight;
    private BitmapSource[] _frames = [];
    private double _elapsed;
    private double? _fpsOverride;
    private int _frameIndex;

    public string CurrentAnimation { get; private set; } = "";
    public int FrameIndex => _frameIndex;
    public bool IsPlaying { get; private set; }
    public bool UsingFallback => _fallbackNames.Contains(CurrentAnimation);
    public double CurrentFps => _fpsOverride ?? (_definitions.TryGetValue(CurrentAnimation, out var definition) ? definition.Fps : 4);
    public BitmapSource CurrentFrame => _frames.Length == 0 ? _fallback("Idle")[0] : _frames[_frameIndex];

    public SpriteAnimationController(SpriteAnimationCatalog catalog, Func<string, BitmapSource[]> fallback, string characterName = "Girl")
    {
        _characterName = characterName;
        _canvasWidth = catalog.CanvasWidth;
        _canvasHeight = catalog.CanvasHeight;
        _fallback = fallback;
        _definitions = catalog.Animations.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var definition in catalog.Animations) _framesByName[definition.Name] = Load(definition);
    }

    public static SpriteAnimationCatalog LoadCatalog(string file)
    {
        try
        {
            return JsonSerializer.Deserialize<SpriteAnimationCatalog>(File.ReadAllText(file),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }
        catch (Exception error)
        {
            LoggerService.Error($"Animation load failure: {Path.GetFileName(file)}", error);
            return new SpriteAnimationCatalog();
        }
    }

    public BitmapSource[] FramesFor(string name) => _framesByName.TryGetValue(name, out var frames) ? frames : _fallback(name);

    public void Play(string name)
    {
        if (!_framesByName.TryGetValue(name, out var next)) next = Fallback(name);
        if (string.Equals(CurrentAnimation, name, StringComparison.OrdinalIgnoreCase)) { IsPlaying = true; return; }
        var shared = ReferenceEquals(_frames, next);
        CurrentAnimation = name;
        _frames = next;
        if (!shared) { _frameIndex = 0; _elapsed = 0; }
        IsPlaying = true;
    }

    public void Pause() => IsPlaying = false;
    public void Stop() { IsPlaying = false; _frameIndex = 0; _elapsed = 0; }
    public void SetFpsOverride(double? fps) => _fpsOverride = fps is > 0 ? fps : null;

    public bool Advance(TimeSpan elapsed)
    {
        if (!IsPlaying || _frames.Length < 2 || CurrentFps <= 0) return false;
        _elapsed += Math.Max(0, elapsed.TotalSeconds);
        var frameDuration = 1 / CurrentFps;
        if (_elapsed < frameDuration) return false;
        var steps = (int)(_elapsed / frameDuration);
        _elapsed -= steps * frameDuration;
        var definition = _definitions.GetValueOrDefault(CurrentAnimation);
        if (definition?.Loop == false && _frameIndex + steps >= _frames.Length)
        {
            _frameIndex = _frames.Length - 1;
            IsPlaying = false;
            if (!string.IsNullOrWhiteSpace(definition.NextAnimation)) Play(definition.NextAnimation);
            return true;
        }
        _frameIndex = (_frameIndex + steps) % _frames.Length;
        return true;
    }

    private BitmapSource[] Load(SpriteAnimationDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.AssetPath) && string.IsNullOrWhiteSpace(definition.FolderPath) && definition.Frames.Length == 0)
            return Fallback(definition.Name);
        var key = !string.IsNullOrWhiteSpace(definition.FolderPath) ? $"folder:{definition.FolderPath}"
            : definition.Frames.Length > 0 ? $"frames:{string.Join('|', definition.Frames)}" : $"sheet:{definition.AssetPath}:{definition.FrameWidth}:{definition.FrameCount}";
        if (_framesByAsset.TryGetValue(key, out var cached)) return cached;
        try
        {
            BitmapSource[] sources;
            if (definition.Frames.Length > 0)
                sources = definition.Frames.Select(x => ReadPng(Resolve(x))).ToArray();
            else if (!string.IsNullOrWhiteSpace(definition.FolderPath))
            {
                var folder = Resolve(definition.FolderPath);
                sources = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.png").OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Select(ReadPng).ToArray() : [];
            }
            else
            {
                var sheet = ReadPng(Resolve(definition.AssetPath));
                var width = definition.FrameWidth > 0 ? definition.FrameWidth : definition.FrameCount > 0 ? sheet.PixelWidth / definition.FrameCount : 0;
                if (width <= 0 || sheet.PixelWidth % width != 0) throw new InvalidDataException("Invalid sprite sheet frame width.");
                var count = definition.FrameCount > 0 ? definition.FrameCount : sheet.PixelWidth / width;
                if (count * width > sheet.PixelWidth) throw new InvalidDataException("Sprite sheet is too short.");
                sources = Enumerable.Range(0, count).Select(i => (BitmapSource)new CroppedBitmap(sheet, new Int32Rect(i * width, 0, width, sheet.PixelHeight))).ToArray();
            }
            if (sources.Length == 0) throw new FileNotFoundException("No PNG frames found.");
            if (definition.FrameCount > 0 && definition.FrameCount != sources.Length) throw new InvalidDataException("Frame count does not match animation config.");
            cached = sources.Select(PadToCanvas).ToArray();
            _framesByAsset[key] = cached;
            LoggerService.Info($"Animation asset loaded: {definition.Name} ({cached.Length} frames)");
            return cached;
        }
        catch (FileNotFoundException)
        {
            LoggerService.Error($"{_characterName} animation asset missing: {definition.Name}");
            return Fallback(definition.Name);
        }
        catch (Exception error)
        {
            LoggerService.Error($"Animation load failure: {definition.Name}", error);
            return Fallback(definition.Name);
        }
    }

    private BitmapSource[] Fallback(string name) { _fallbackNames.Add(name); return _fallback(name); }
    private static string Resolve(string relative) => Path.Combine(AppContext.BaseDirectory, relative.Replace('/', Path.DirectorySeparatorChar));
    private static BitmapSource ReadPng(string file)
    {
        if (!File.Exists(file)) throw new FileNotFoundException("Animation PNG missing.", file);
        var bitmap = new BitmapImage();
        bitmap.BeginInit(); bitmap.UriSource = new Uri(file); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.EndInit(); bitmap.Freeze();
        return bitmap;
    }
    private BitmapSource PadToCanvas(BitmapSource source)
    {
        if (source.PixelWidth > _canvasWidth || source.PixelHeight > _canvasHeight)
            throw new InvalidDataException($"Frame {source.PixelWidth}x{source.PixelHeight} exceeds canvas {_canvasWidth}x{_canvasHeight}.");
        var converted = new FormatConvertedBitmap(source, System.Windows.Media.PixelFormats.Bgra32, null, 0);
        var rowBytes = source.PixelWidth * 4;
        var raw = new byte[rowBytes * source.PixelHeight];
        converted.CopyPixels(raw, rowBytes, 0);
        var canvasStride = _canvasWidth * 4;
        var canvas = new byte[canvasStride * _canvasHeight];
        var top = _canvasHeight - source.PixelHeight;
        for (var y = 0; y < source.PixelHeight; y++) Buffer.BlockCopy(raw, y * rowBytes, canvas, (top + y) * canvasStride, rowBytes);
        var result = BitmapSource.Create(_canvasWidth, _canvasHeight, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, canvas, canvasStride);
        result.Freeze();
        return result;
    }
}
