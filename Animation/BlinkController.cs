using DeskLofi.Models;

namespace DeskLofi.Animation;

public enum BlinkFrame { Open, Half, Closed }

/// <summary>Idle-only blink scheduler driven by the existing window clock.</summary>
public sealed class BlinkController : IDisposable
{
    private readonly AppSettings _settings;
    private readonly Random _random;
    private bool _canBlink;
    private bool _disposed;
    private int _phase;
    private double _remaining;

    public BlinkFrame Frame { get; private set; } = BlinkFrame.Open;
    public bool IsBlinking => _phase is > 0 and < 4;
    public double? NextBlinkInSeconds => _canBlink && !IsBlinking ? Math.Max(0, _remaining) : null;

    public BlinkController(AppSettings settings, Random? random = null) { _settings = settings; _random = random ?? Random.Shared; }

    public void SetCanBlink(bool canBlink)
    {
        canBlink = !_disposed && _settings.EnableBlink && canBlink;
        if (_canBlink == canBlink) return;
        _canBlink = canBlink; Frame = BlinkFrame.Open; _phase = 0;
        _remaining = canBlink ? RandomInterval() : 0;
    }

    public bool BlinkNow()
    {
        if (_disposed || !_settings.EnableBlink || !_canBlink) return false;
        _phase = 1; Frame = BlinkFrame.Half; _remaining = 0.06; return true;
    }

    public bool Advance(TimeSpan elapsed)
    {
        if (_disposed || !_canBlink || !_settings.EnableBlink) return false;
        var remaining = Math.Max(0, elapsed.TotalSeconds); var changed = false;
        while (remaining >= _remaining)
        {
            remaining -= _remaining;
            switch (_phase)
            {
                case 0: _phase = 1; Frame = BlinkFrame.Half; _remaining = 0.06; break;
                case 1: _phase = 2; Frame = BlinkFrame.Closed; _remaining = 0.08; break;
                case 2: _phase = 3; Frame = BlinkFrame.Half; _remaining = 0.06; break;
                case 3: _phase = 0; Frame = BlinkFrame.Open; _remaining = RandomInterval(); break;
            }
            changed = true;
        }
        _remaining -= remaining; return changed;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _canBlink = false; _phase = 0; _remaining = 0; Frame = BlinkFrame.Open;
    }

    private double RandomInterval()
    {
        var min = Math.Clamp(_settings.MinBlinkIntervalSeconds, 1, 120);
        var max = Math.Clamp(_settings.MaxBlinkIntervalSeconds, min, 120);
        return _random.Next(min, max + 1);
    }
}