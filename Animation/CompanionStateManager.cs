using DeskLofi.Models;
using DeskLofi.Services;

namespace DeskLofi.Animation;

/// <summary>Tracks real user activity separately from Girl's scheduled visual action.</summary>
public sealed class CompanionStateManager
{
    public const int FirstActionMinSeconds = 10;
    public const int FirstActionMaxSeconds = 20;
    public const int CoffeeMinSeconds = 20;
    public const int CoffeeMaxSeconds = 40;
    public const int StretchMinSeconds = 30;
    public const int StretchMaxSeconds = 60;
    public const int ActionGapMinSeconds = 5;
    public const int ActionGapMaxSeconds = 10;
    public const int FastTypingDelayMinSeconds = 2;
    public const int FastTypingDelayMaxSeconds = 8;

    private readonly ActivityTracker _activity;
    private readonly AppSettings _settings;
    private readonly Func<GirlState> _chooseAction;
    private readonly Func<GirlState, bool> _hasAnimation;
    private readonly Random _random;
    private DateTime _nextCoffeeAt;
    private DateTime _nextStretchAt;
    private DateTime _nextActionAllowedAt;
    private GirlAction _pendingAction;
    private DateTime _pendingUntil;
    private bool _fastTyping;

    public UserActivityState Activity { get; private set; } = UserActivityState.Idle;
    public GirlAction Action { get; private set; } = GirlAction.None;
    public GirlState Girl => Action switch
    {
        GirlAction.Coffee => GirlState.Coffee,
        GirlAction.Stretch => GirlState.Stretch,
        _ => Activity switch
        {
            UserActivityState.Typing when _fastTyping => GirlState.TypingFast,
            UserActivityState.Typing => GirlState.Typing,
            UserActivityState.Mouse => GirlState.Mouse,
            _ => GirlState.Idle
        }
    };
    public DateTime NextCoffeeAt => _nextCoffeeAt;
    public DateTime NextStretchAt => _nextStretchAt;
    public DateTime NextActionAllowedAt => _nextActionAllowedAt;
    public event Action<GirlState>? StateChanged;

    public CompanionStateManager(ActivityTracker activity, AppSettings settings, Func<GirlState>? chooseIdleSpecial = null,
        Func<GirlState, bool>? hasAnimation = null, Random? random = null, DateTime? startedAt = null)
    {
        _activity = activity;
        _settings = settings;
        _random = random ?? Random.Shared;
        _chooseAction = chooseIdleSpecial ?? (() => _random.Next(2) == 0 ? GirlState.Coffee : GirlState.Stretch);
        _hasAnimation = hasAnimation ?? (_ => true);
        var firstAt = (startedAt ?? DateTime.UtcNow).AddSeconds(_random.Next(FirstActionMinSeconds, FirstActionMaxSeconds + 1));
        _nextCoffeeAt = firstAt;
        _nextStretchAt = firstAt;
        _nextActionAllowedAt = firstAt;
    }

    public void Tick(DateTime? at = null)
    {
        var now = at ?? DateTime.UtcNow;
        var previousActivity = Activity;
        var previousAction = Action;
        var previousGirl = Girl;
        ResolveActivity(now);
        ScheduleAction(now);
        if (previousActivity != Activity || previousAction != Action || previousGirl != Girl)
            StateChanged?.Invoke(Girl);
    }

    public bool ForceAction(GirlAction action)
    {
        if (action is not (GirlAction.Coffee or GirlAction.Stretch) || Action != GirlAction.None ||
            !_hasAnimation(ToGirlState(action))) return false;
        _pendingAction = GirlAction.None;
        Action = action;
        LoggerService.Info($"Girl action forced: {action}.");
        StateChanged?.Invoke(Girl);
        return true;
    }

    public void CompleteAction(DateTime? at = null)
    {
        if (Action == GirlAction.None) return;
        var now = at ?? DateTime.UtcNow;
        var completed = Action;
        Action = GirlAction.None;
        _nextActionAllowedAt = now.AddSeconds(_random.Next(ActionGapMinSeconds, ActionGapMaxSeconds + 1));
        if (completed == GirlAction.Coffee)
            _nextCoffeeAt = now.AddSeconds(_random.Next(CoffeeMinSeconds, CoffeeMaxSeconds + 1));
        else
            _nextStretchAt = now.AddSeconds(_random.Next(StretchMinSeconds, StretchMaxSeconds + 1));
        LoggerService.Info($"Girl action completed: {completed}.");
        ResolveActivity(now);
        StateChanged?.Invoke(Girl);
    }

    public void CompleteIdleSpecial(DateTime? at = null) => CompleteAction(at);
    public void CompleteCoffee(DateTime? at = null)
    {
        if (Action == GirlAction.Coffee) CompleteAction(at);
    }

    private void ResolveActivity(DateTime now)
    {
        var keyboard = _activity.LastKeyboardActivity;
        var mouse = _activity.LastMouseActivity;
        var keyboardAge = now - keyboard;
        var mouseAge = now - mouse;
        var keyboardRecent = _settings.ReactToKeyboard && keyboard != DateTime.MinValue &&
            keyboardAge <= TimeSpan.FromMilliseconds(Math.Max(0, _settings.TypingIdleDelayMs));
        var mouseRecent = _settings.ReactToMouse && mouse != DateTime.MinValue &&
            mouseAge <= TimeSpan.FromMilliseconds(Math.Max(0, _settings.MouseIdleDelayMs));
        var keyboardPriority = keyboardRecent && keyboardAge <=
            TimeSpan.FromMilliseconds(Math.Max(0, _settings.KeyboardPriorityWindowMs));
        var typingAvailable = _hasAnimation(GirlState.Typing);
        var mouseAvailable = _hasAnimation(GirlState.Mouse);
        Activity = (keyboardPriority || keyboardRecent && (!mouseRecent || keyboard >= mouse)) && typingAvailable
            ? UserActivityState.Typing
            : mouseRecent && mouseAvailable ? UserActivityState.Mouse : UserActivityState.Idle;
        var typingWindow = TimeSpan.FromMilliseconds(Math.Max(100, _settings.FastTypingWindowMs));
        _fastTyping = Activity == UserActivityState.Typing &&
            _activity.KeyboardEventsPerSecond(now, typingWindow) >= _settings.FastTypingThresholdPerSecond;
    }

    private void ScheduleAction(DateTime now)
    {
        if (Action != GirlAction.None) return;
        if (_pendingAction != GirlAction.None)
        {
            if (!_fastTyping || now >= _pendingUntil) StartAction(_pendingAction);
            return;
        }
        if (now < _nextActionAllowedAt) return;
        var coffeeDue = now >= _nextCoffeeAt && _hasAnimation(GirlState.Coffee);
        var stretchDue = now >= _nextStretchAt && _hasAnimation(GirlState.Stretch);
        if (!coffeeDue && !stretchDue) return;
        var candidate = coffeeDue && stretchDue
            ? _chooseAction() == GirlState.Stretch ? GirlAction.Stretch : GirlAction.Coffee
            : coffeeDue ? GirlAction.Coffee : GirlAction.Stretch;
        if (_fastTyping)
        {
            _pendingAction = candidate;
            _pendingUntil = now.AddSeconds(_random.Next(FastTypingDelayMinSeconds, FastTypingDelayMaxSeconds + 1));
            return;
        }
        StartAction(candidate);
    }

    private void StartAction(GirlAction action)
    {
        _pendingAction = GirlAction.None;
        Action = action;
        LoggerService.Info($"Girl action started: {action}.");
    }

    private static GirlState ToGirlState(GirlAction action) => action == GirlAction.Coffee ? GirlState.Coffee : GirlState.Stretch;
}
