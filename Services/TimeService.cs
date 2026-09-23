using System.Windows.Threading;
using DeskLofi.Models;

namespace DeskLofi.Services;

/// <summary>Reads local Windows time and publishes changes at a low frequency.</summary>
public sealed class TimeService : IDisposable
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly List<TimePeriodDefinition> _schedule =
    [
        new(TimePeriod.Morning, 5, 8), new(TimePeriod.Day, 8, 17),
        new(TimePeriod.Evening, 17, 20), new(TimePeriod.Night, 20, 5)
    ];
    public DateTime CurrentTime { get; private set; } = DateTime.Now;
    public DateOnly CurrentDate => DateOnly.FromDateTime(CurrentTime);
    public TimePeriod CurrentPeriod { get; private set; }
    public event Action? Updated;

    public TimeService()
    {
        CurrentPeriod = ResolvePeriod(CurrentTime);
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
    }

    public void Configure(IEnumerable<TimePeriodDefinition> definitions)
    {
        var candidate = definitions.ToList();
        if (candidate.Count != 0) _schedule.Clear();
        _schedule.AddRange(candidate);
        Refresh();
    }

    public void Refresh(DateTime? localTime = null)
    {
        var oldPeriod = CurrentPeriod;
        CurrentTime = localTime ?? DateTime.Now;
        CurrentPeriod = ResolvePeriod(CurrentTime);
        Updated?.Invoke();
        if (oldPeriod != CurrentPeriod) PeriodChanged?.Invoke(CurrentPeriod);
    }

    public event Action<TimePeriod>? PeriodChanged;

    private TimePeriod ResolvePeriod(DateTime time)
    {
        var clock = TimeOnly.FromDateTime(time);
        return _schedule.FirstOrDefault(x => x.Contains(clock))?.Period ?? TimePeriod.Night;
    }

    public void Dispose() => _timer.Stop();
}
