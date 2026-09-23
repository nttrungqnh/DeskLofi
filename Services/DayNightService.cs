using System.Windows.Threading;
using DeskLofi.Models;

namespace DeskLofi.Services;

/// <summary>Composes one stable room with a short crossfade between period layers.</summary>
public sealed class DayNightService : IDisposable
{
    private readonly TimeService _time;
    private readonly DispatcherTimer _transition = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private DateTime _transitionStart;
    private TimePeriod _from, _to;
    private bool _debug;
    private TimePeriod? _debugPeriod;
    public TimePeriod CurrentPeriod { get; private set; }
    public double Progress { get; private set; } = 1;
    public TimeSpan TransitionDuration { get; set; } = TimeSpan.FromSeconds(3);
    public event Action<TimePeriod, TimePeriod, double>? TransitionUpdated;

    public DayNightService(TimeService time, bool autoDayNight)
    {
        _time = time;
        CurrentPeriod = time.CurrentPeriod;
        _time.PeriodChanged += OnPeriodChanged;
        _transition.Tick += Advance;
        IsEnabled = autoDayNight;
    }
    public bool IsEnabled { get; set; }
    public void SetDebugPeriod(TimePeriod? period)
    {
        _debugPeriod = period; _debug = period.HasValue;
        if (_debug && period is { } p) BeginTransition(p);
        else if (!_debug) BeginTransition(_time.CurrentPeriod);
    }
    public void RefreshSetting(bool enabled)
    {
        IsEnabled = enabled;
        if (enabled && !_debug) BeginTransition(_time.CurrentPeriod);
    }
    private void OnPeriodChanged(TimePeriod period) { if (IsEnabled && !_debug) BeginTransition(period); }
    private void BeginTransition(TimePeriod target)
    {
        if (target == CurrentPeriod && Progress >= 1) return;
        _from = CurrentPeriod; _to = target; _transitionStart = DateTime.UtcNow; Progress = 0;
        if (TransitionDuration <= TimeSpan.Zero) { CurrentPeriod = target; Progress = 1; TransitionUpdated?.Invoke(_from, _to, 1); return; }
        if (!_transition.IsEnabled) _transition.Start();
    }
    private void Advance(object? sender, EventArgs e)
    {
        Progress = Math.Clamp((DateTime.UtcNow - _transitionStart).TotalMilliseconds / TransitionDuration.TotalMilliseconds, 0, 1);
        if (Progress >= 1) { CurrentPeriod = _to; _transition.Stop(); }
        TransitionUpdated?.Invoke(_from, _to, Progress);
    }
    public void Dispose() { _time.PeriodChanged -= OnPeriodChanged; _transition.Stop(); }
}
