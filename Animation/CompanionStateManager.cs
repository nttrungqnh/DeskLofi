using DeskLofi.Models;
using DeskLofi.Services;

namespace DeskLofi.Animation;
public sealed class CompanionStateManager
{
    private readonly ActivityTracker _activity; private readonly AppSettings _settings; private bool _catSleeping;
    private DateTime _catReactionUntil; private CatState _catReaction=CatState.LookAtGirl;
    public GirlState Girl { get; private set; } = GirlState.Idle;
    public CatState Cat { get; private set; } = CatState.Idle;
    public event Action<GirlState, CatState>? StateChanged;
    public CompanionStateManager(ActivityTracker activity, AppSettings settings) { _activity = activity; _settings = settings; }
    public void ReactToCat(bool doubleClick=false) { _catReaction=doubleClick?CatState.Stretch:CatState.LookAtGirl;_catReactionUntil=DateTime.UtcNow.AddSeconds(2); }
    public void Tick(DateTime? at = null)
    {
        var now = at ?? DateTime.UtcNow; var idle = now - _activity.LastActivity;
        var typingWindow = TimeSpan.FromMilliseconds(Math.Max(100, _settings.FastTypingWindowMs));
        var keyboardRate = _activity.KeyboardEventsPerSecond(now, typingWindow);
        var girl = ResolveGirl(now, keyboardRate);
        var sleeping = idle.TotalSeconds >= _settings.CatSleepTimeoutSeconds;
        CatState cat = sleeping ? CatState.Sleep : _catSleeping ? CatState.WakeUp : keyboardRate > 0 ? CatState.TailWag : CatState.Idle;
        if(now<_catReactionUntil&&!sleeping)cat=_catReaction;
        _catSleeping = sleeping;
        if (Girl != girl || Cat != cat) { Girl = girl; Cat = cat; StateChanged?.Invoke(girl, cat); }
    }

    private GirlState ResolveGirl(DateTime now, double keyboardRate)
    {
        var lastKeyboard = _activity.LastKeyboardActivity;
        var lastMouse = _activity.LastMouseActivity;
        var keyboardAge = now - lastKeyboard;
        var mouseAge = now - lastMouse;
        var keyboardRecent = _settings.ReactToKeyboard && lastKeyboard != DateTime.MinValue &&
            keyboardAge <= TimeSpan.FromMilliseconds(Math.Max(0, _settings.TypingIdleDelayMs));
        var mouseRecent = _settings.ReactToMouse && lastMouse != DateTime.MinValue &&
            mouseAge <= TimeSpan.FromMilliseconds(Math.Max(0, _settings.MouseIdleDelayMs));
        var keyboardPriority = keyboardRecent &&
            keyboardAge <= TimeSpan.FromMilliseconds(Math.Max(0, _settings.KeyboardPriorityWindowMs));

        if (keyboardPriority || keyboardRecent && (!mouseRecent || lastKeyboard >= lastMouse))
            return keyboardRate >= _settings.FastTypingThresholdPerSecond ? GirlState.TypingFast : GirlState.Typing;
        if (mouseRecent) return GirlState.Mouse;
        return GirlState.Idle;
    }
}
