using DeskLofi.Models;
using DeskLofi.Services;

namespace DeskLofi.Animation;
public sealed class CompanionStateManager
{
    private readonly ActivityTracker _activity; private readonly AppSettings _settings; private bool _wasAfk, _catSleeping; private GirlState _afkState=GirlState.Away; private DateTime _nextMicro = DateTime.UtcNow.AddSeconds(24);
    private DateTime _girlReactionUntil, _catReactionUntil; private GirlState _girlReaction=GirlState.LookAtCat; private CatState _catReaction=CatState.LookAtGirl;
    private static readonly Random Rng = new();
    public GirlState Girl { get; private set; } = GirlState.Idle;
    public CatState Cat { get; private set; } = CatState.Idle;
    public event Action<GirlState, CatState>? StateChanged;
    public CompanionStateManager(ActivityTracker activity, AppSettings settings) { _activity = activity; _settings = settings; }
    public void ReactToGirl() { _girlReaction=GirlState.LookAtCat;_girlReactionUntil=DateTime.UtcNow.AddSeconds(2); }
    public void ReactToCat(bool doubleClick=false) { _catReaction=doubleClick?CatState.Stretch:CatState.LookAtGirl;_catReactionUntil=DateTime.UtcNow.AddSeconds(2); }
    public void Tick(DateTime? at = null)
    {
        var now = at ?? DateTime.UtcNow; var idle = now - _activity.LastActivity; var afk = idle.TotalMinutes >= _settings.AfkTimeoutMinutes;
        var typingWindow = TimeSpan.FromMilliseconds(Math.Max(100, _settings.FastTypingWindowMs));
        var keyboardRate = _activity.KeyboardEventsPerSecond(now, typingWindow);
        var typing = _settings.ReactToKeyboard && _activity.LastKeyboardActivity != DateTime.MinValue
            && now - _activity.LastKeyboardActivity <= TimeSpan.FromMilliseconds(Math.Max(0, _settings.TypingIdleDelayMs));
        if(afk&&!_wasAfk) _afkState=new[]{GirlState.Away,GirlState.Coffee,GirlState.Stretch,GirlState.LookWindow}[Rng.Next(4)];
        GirlState girl;
        if (typing) girl = keyboardRate >= _settings.FastTypingThresholdPerSecond ? GirlState.TypingFast : GirlState.Typing;
        else if (_wasAfk && !afk) girl = GirlState.SitDown;
        else if (afk) girl = _afkState;
        else if (_settings.ReactToMouse && (now - _activity.LastMouseActivity).TotalSeconds < 2.5) girl = GirlState.Mouse;
        else girl = GirlState.Idle;
        var sleeping = idle.TotalSeconds >= _settings.CatSleepTimeoutSeconds;
        CatState cat = sleeping ? CatState.Sleep : _catSleeping ? CatState.WakeUp : keyboardRate > 0 ? CatState.TailWag : CatState.Idle;
        if (girl == GirlState.Idle && now >= _nextMicro) { girl = (GirlState) new[] { (GirlState)0,(GirlState)0,(GirlState)0,GirlState.Coffee,GirlState.LookWindow,GirlState.Stretch,GirlState.LookAtCat }[Rng.Next(7)]; _nextMicro = now.AddSeconds(Rng.Next(35, 80)); }
        if(now<_girlReactionUntil&&!afk&&!typing&&girl==GirlState.Idle)girl=_girlReaction;
        if(now<_catReactionUntil&&!sleeping)cat=_catReaction;
        if (girl == GirlState.SitDown) _nextMicro = now.AddSeconds(1);
        _wasAfk = afk; _catSleeping = sleeping;
        if (Girl != girl || Cat != cat) { Girl = girl; Cat = cat; StateChanged?.Invoke(girl, cat); }
    }
}
