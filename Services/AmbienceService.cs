using System.Windows.Media;
using System.Windows.Threading;
using DeskLofi.Models;

namespace DeskLofi.Services;

/// <summary>Independent ambient audio channel; it never seeks, pauses or replaces music.</summary>
public sealed class AmbienceService : IDisposable
{
    private readonly MediaPlayer _rain = new();
    private readonly MediaPlayer _thunder = new();
    private readonly DispatcherTimer _fade = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly string _root = Path.Combine(AppContext.BaseDirectory, "Assets", "Audio", "Ambience");
    private string? _activePath, _targetPath;
    private double _volume = .22, _targetVolume;
    public bool IsPlaying => _activePath is not null;
    public AmbienceService(double volume)
    {
        _volume = Math.Clamp(volume, 0, 1); _rain.Volume = 0; _thunder.Volume = _volume;
        _rain.MediaEnded += (_, _) => { if (_activePath is not null) { try { _rain.Position = TimeSpan.Zero; _rain.Play(); } catch { } } };
        _fade.Tick += FadeTick;
    }
    public void SetVolume(double volume) { _volume = Math.Clamp(volume, 0, 1); if (_targetPath is not null) _targetVolume = _volume; _thunder.Volume = _volume; }
    public void Apply(WeatherState state, bool autoMode)
    {
        var name = !autoMode ? null : state switch
        {
            WeatherState.Drizzle => "rain_light", WeatherState.Rain => "rain_medium",
            WeatherState.HeavyRain or WeatherState.Thunderstorm => "rain_heavy",
            WeatherState.Cloudy or WeatherState.Fog or WeatherState.Snow => "wind", _ => null
        };
        var path = name is null ? null : FindAudio(name);
        _targetPath = path;
        _targetVolume = path is null ? 0 : _volume;
        if (path is not null && _activePath != path && _rain.Volume <= .001) OpenTarget();
        if (!_fade.IsEnabled) _fade.Start();
    }
    public void PlayThunder()
    {
        var path = FindAudio("thunder"); if (path is null) return;
        try { _thunder.Stop(); _thunder.Open(new Uri(path)); _thunder.Play(); }
        catch (Exception ex) { LoggerService.Error("Could not play thunder ambience", ex); }
    }
    private void FadeTick(object? sender, EventArgs e)
    {
        var delta = Math.Max(.005, _volume / 25);
        if (_activePath != _targetPath && _rain.Volume > .001)
        {
            _rain.Volume = Math.Max(0, _rain.Volume - delta);
            if (_rain.Volume <= .001) { _rain.Stop(); _activePath = null; if (_targetPath is not null) OpenTarget(); }
            return;
        }
        _rain.Volume = _rain.Volume < _targetVolume ? Math.Min(_targetVolume, _rain.Volume + delta) : Math.Max(_targetVolume, _rain.Volume - delta);
        if (_activePath is null && _targetPath is null && _rain.Volume <= .001) { _rain.Stop(); _fade.Stop(); }
    }
    private void OpenTarget()
    {
        if (_targetPath is null) return;
        try { _rain.Open(new Uri(_targetPath)); _activePath = _targetPath; _rain.Volume = 0; _rain.Play(); }
        catch (Exception ex) { LoggerService.Error("Could not open weather ambience", ex); _activePath = null; _targetPath = null; }
    }
    private string? FindAudio(string basename)
    {
        foreach (var ext in new[] { ".wav", ".mp3", ".wma" }) { var file = Path.Combine(_root, basename + ext); if (File.Exists(file)) return file; }
        return null;
    }
    public void Dispose() { _fade.Stop(); _rain.Stop(); _rain.Close(); _thunder.Stop(); _thunder.Close(); }
}
