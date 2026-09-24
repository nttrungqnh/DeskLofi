namespace DeskLofi.Services;
public sealed class ActivityTracker
{
    private readonly Queue<DateTime> _keys = new();
    private readonly DateTime _startedAt = DateTime.UtcNow;
    private long _lastMouseTicks;
    public DateTime LastKeyboardActivity { get; private set; } = DateTime.MinValue;
    public DateTime LastMouseActivity
    {
        get
        {
            var ticks = Interlocked.Read(ref _lastMouseTicks);
            return ticks == 0 ? DateTime.MinValue : new DateTime(ticks, DateTimeKind.Utc);
        }
    }
    public DateTime LastActivity => LastKeyboardActivity == DateTime.MinValue && LastMouseActivity == DateTime.MinValue
        ? _startedAt : LastKeyboardActivity > LastMouseActivity ? LastKeyboardActivity : LastMouseActivity;
    public int KeyboardRate => KeyboardEvents(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    public double KeyboardEventsPerSecond(DateTime now, TimeSpan window) => KeyboardEvents(now, window) / Math.Max(0.001, window.TotalSeconds);
    public int KeyboardEvents(DateTime now, TimeSpan window)
    {
        while (_keys.Count > 0 && now - _keys.Peek() > window) _keys.Dequeue();
        return _keys.Count;
    }
    public void Keyboard(DateTime at) { LastKeyboardActivity = at; _keys.Enqueue(at); }
    public void Mouse(DateTime at) => Interlocked.Exchange(ref _lastMouseTicks, at.Ticks);
}
