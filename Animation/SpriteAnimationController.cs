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
    private int _pingPongDirection = 1;
    private bool _sceneActive = true;
    private int _blinkStep;
    private double _eventRemaining;

    private static readonly Random Random = new();

    public string CurrentAnimation { get; private set; } = "";
    public int FrameIndex => _frameIndex;
    public int FrameCount => _frames.Length;
    public bool IsPlaying { get; private set; }
    public bool UsingFallback => _fallbackNames.Contains(CurrentAnimation);
    public double CurrentFps => _fpsOverride ?? (_definitions.TryGetValue(CurrentAnimation, out var definition) ? definition.Fps : 4);
    public AnimationMode CurrentMode => _definitions.TryGetValue(CurrentAnimation, out var definition) ? definition.Mode : AnimationMode.Loop;
    public double? NextBlinkInSeconds => CurrentMode == AnimationMode.IdleWithRandomBlink && _blinkStep is 0 or 4 ? Math.Max(0, _eventRemaining) : null;
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
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } }) ?? new();
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
        if (!shared) { _frameIndex = 0; _elapsed = 0; _pingPongDirection = 1; }
        ResetBlinkSchedule();
        IsPlaying = true;
    }

    public void Pause() => IsPlaying = false;
    public void Stop() { IsPlaying = false; _frameIndex = 0; _elapsed = 0; _pingPongDirection = 1; }
    public void SetFpsOverride(double? fps) => _fpsOverride = fps is > 0 ? fps : null;

    public void SetSceneActive(bool active)
    {
        if (_sceneActive == active) return;
        _sceneActive = active;
        if (active && CurrentMode == AnimationMode.IdleWithRandomBlink)
        {
            _frameIndex = 0;
            ResetBlinkSchedule();
        }
    }

    public void BlinkNow()
    {
        if (CurrentMode != AnimationMode.IdleWithRandomBlink || _frames.Length < 4 || !_sceneActive) return;
        _frameIndex = 1;
        _blinkStep = 1;
        var definition = CurrentDefinition!;
        _eventRemaining = NextDuration(definition.BlinkFrameDurationMinMs, definition.BlinkFrameDurationMaxMs) / 1000d;
    }

    public bool Advance(TimeSpan elapsed)
    {
        if (!IsPlaying || !_sceneActive || _frames.Length < 2) return false;
        var definition = CurrentDefinition;
        if (definition?.Mode == AnimationMode.IdleWithRandomBlink) return _frames.Length < 4 ? false : AdvanceBlink(elapsed, definition);
        if (CurrentFps <= 0) return false;
        _elapsed += Math.Max(0, elapsed.TotalSeconds);
        var frameDuration = 1 / CurrentFps;
        if (_elapsed < frameDuration) return false;
        var steps = (int)(_elapsed / frameDuration);
        _elapsed -= steps * frameDuration;
        if (definition?.Mode == AnimationMode.PingPong)
        {
            for (var i = 0; i < steps; i++)
            {
                var next = _frameIndex + _pingPongDirection;
                if (next >= _frames.Length) { _pingPongDirection = -1; next = _frames.Length - 2; }
                else if (next < 0) { _pingPongDirection = 1; next = 1; }
                _frameIndex = next;
            }
            return true;
        }
        if ((definition?.Mode == AnimationMode.OneShot || definition?.Loop == false) && _frameIndex + steps >= _frames.Length)
        {
            _frameIndex = _frames.Length - 1;
            IsPlaying = false;
            if (!string.IsNullOrWhiteSpace(definition.NextAnimation)) Play(definition.NextAnimation);
            return true;
        }
        _frameIndex = (_frameIndex + steps) % _frames.Length;
        return true;
    }

    private SpriteAnimationDefinition? CurrentDefinition => _definitions.GetValueOrDefault(CurrentAnimation);

    private bool AdvanceBlink(TimeSpan elapsed, SpriteAnimationDefinition definition)
    {
        var remaining = Math.Max(0, elapsed.TotalSeconds);
        var changed = false;
        while (remaining >= _eventRemaining)
        {
            remaining -= _eventRemaining;
            if (_blinkStep == 0)
            {
                _frameIndex = 1;
                _blinkStep = 1;
                _eventRemaining = NextDuration(definition.BlinkFrameDurationMinMs, definition.BlinkFrameDurationMaxMs) / 1000d;
            }
            else if (_blinkStep < 3)
            {
                _frameIndex = ++_blinkStep;
                _eventRemaining = NextDuration(definition.BlinkFrameDurationMinMs, definition.BlinkFrameDurationMaxMs) / 1000d;
            }
            else if (_blinkStep == 3)
            {
                _frameIndex = 0;
                _blinkStep = Random.NextDouble() < Math.Clamp(definition.DoubleBlinkChance, 0, 1) ? 4 : 0;
                _eventRemaining = _blinkStep == 4
                    ? NextDuration(definition.DoubleBlinkDelayMinMs, definition.DoubleBlinkDelayMaxMs) / 1000d
                    : NextDuration(definition.BlinkDelayMinMs, definition.BlinkDelayMaxMs) / 1000d;
            }
            else
            {
                _frameIndex = 1;
                _blinkStep = 1;
                _eventRemaining = NextDuration(definition.BlinkFrameDurationMinMs, definition.BlinkFrameDurationMaxMs) / 1000d;
            }
            changed = true;
        }
        _eventRemaining -= remaining;
        return changed;
    }

    private void ResetBlinkSchedule()
    {
        _blinkStep = 0;
        _eventRemaining = CurrentDefinition?.Mode == AnimationMode.IdleWithRandomBlink
            ? NextDuration(CurrentDefinition.BlinkDelayMinMs, CurrentDefinition.BlinkDelayMaxMs) / 1000d
            : 0;
    }

    private static int NextDuration(int min, int max)
    {
        min = Math.Max(1, min);
        max = Math.Max(min, max);
        return Random.Next(min, max + 1);
    }

    private BitmapSource[] Load(SpriteAnimationDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.AssetPath) && string.IsNullOrWhiteSpace(definition.FolderPath) && definition.Frames.Length == 0)
            return Fallback(definition.Name);
        var sourceKey = definition.Frames.Length > 0 ? $"frames:{string.Join('|', definition.Frames)}"
            : !string.IsNullOrWhiteSpace(definition.FolderPath) ? $"folder:{definition.FolderPath}"
            : $"sheet:{definition.AssetPath}:{definition.FrameWidth}:{definition.FrameHeight}:{definition.StartX}:{definition.StartY}:{definition.FrameGap}:{definition.FrameCount}";
        var key = $"{sourceKey}:{definition.SourceScale}:{definition.HorizontalAnchor}:{definition.VerticalAnchor}:{definition.OffsetX}:{definition.OffsetY}";
        if (_framesByAsset.TryGetValue(key, out var cached)) return cached;
        try
        {
            BitmapSource[] sources;
            if (definition.Frames.Length > 0)
                sources = MultiFrameAnimationSource.Load(definition.Frames.Select(Resolve), definition, _canvasWidth, _canvasHeight);
            else if (!string.IsNullOrWhiteSpace(definition.FolderPath))
            {
                var folder = Resolve(definition.FolderPath);
                sources = Directory.Exists(folder) ? MultiFrameAnimationSource.Load(Directory.GetFiles(folder, "*.png").OrderBy(x => x, StringComparer.OrdinalIgnoreCase), definition, _canvasWidth, _canvasHeight) : [];
            }
            else
            {
                var sheet = MultiFrameAnimationSource.ReadPng(Resolve(definition.AssetPath));
                var width = definition.FrameWidth > 0 ? definition.FrameWidth : definition.FrameCount > 0 ? (sheet.PixelWidth - definition.StartX - Math.Max(0, definition.FrameCount - 1) * definition.FrameGap) / definition.FrameCount : 0;
                var height = definition.FrameHeight > 0 ? definition.FrameHeight : sheet.PixelHeight - definition.StartY;
                var count = definition.FrameCount > 0 ? definition.FrameCount : width > 0 ? (sheet.PixelWidth - definition.StartX + definition.FrameGap) / (width + definition.FrameGap) : 0;
                if (width <= 0 || height <= 0 || count <= 0) throw new InvalidDataException("Invalid sprite sheet frame dimensions.");
                sources = Enumerable.Range(0, count).Select(i =>
                {
                    var x = definition.StartX + i * (width + definition.FrameGap);
                    if (x < 0 || definition.StartY < 0 || x + width > sheet.PixelWidth || definition.StartY + height > sheet.PixelHeight)
                        throw new InvalidDataException("Sprite frame lies outside the sheet.");
                    return MultiFrameAnimationSource.PlaceOnCanvas(new CroppedBitmap(sheet, new Int32Rect(x, definition.StartY, width, height)), definition, _canvasWidth, _canvasHeight);
                }).ToArray();
            }
            if (sources.Length == 0) throw new FileNotFoundException("No PNG frames found.");
            if (definition.FrameCount > 0 && definition.FrameCount != sources.Length) throw new InvalidDataException("Frame count does not match animation config.");
            if (definition.Mode == AnimationMode.IdleWithRandomBlink && sources.Length < 4) throw new InvalidDataException("Idle blink animations require at least four frames.");
            cached = sources;
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
}
