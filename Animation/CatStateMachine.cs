using DeskLofi.Models;

namespace DeskLofi.Animation;

/// <summary>Owns Cat idle, tail, and sleep transitions on the scene's shared animation clock.</summary>
public sealed class CatStateMachine
{
    public const int DefaultSleepTimeoutSeconds = AppSettings.DefaultCatSleepTimeoutSeconds;
    public const int ReactAfterIdleSeconds = 30;
    private readonly int[] _tailSequence;
    private const double SleepFrameSeconds = .38;
    private const double WakeFrameSeconds = .24;

    private readonly Random _random;
    private readonly int _sleepTimeoutSeconds;
    private readonly int _sleepFrameCount;
    private readonly int _tailFrameCount;
    private double _tailSecondsRemaining;
    private double _secondsSinceActivity;
    private double _frameElapsed;
    private int _tailSequenceIndex;
    private DateTime _lastActivityAt;

    public CatState State { get; private set; } = CatState.Idle;
    public int TailFrameIndex { get; private set; } = -1;
    public int SleepFrameIndex { get; private set; } = -1;
    public event Action<CatState>? StateChanged;

    public CatStateMachine(DateTime? startedAt = null, Random? random = null, int sleepTimeoutSeconds = DefaultSleepTimeoutSeconds,
        int tailFrameCount = 4, int sleepFrameCount = 3)
    {
        _random = random ?? Random.Shared;
        _sleepTimeoutSeconds = Math.Max(1, sleepTimeoutSeconds);
        _sleepFrameCount = Math.Max(0, sleepFrameCount);
        var tailCount = Math.Max(0, tailFrameCount);
        _tailFrameCount = tailCount;
        _tailSequence = tailCount == 0 ? [] : Enumerable.Range(0, tailCount)
            .Concat(Enumerable.Range(0, tailCount - 1).Reverse()).ToArray();
        _lastActivityAt = startedAt ?? DateTime.UtcNow;
        _secondsSinceActivity = Math.Max(0, (DateTime.UtcNow - _lastActivityAt).TotalSeconds);
        ScheduleNextTailWag();
    }

    /// <summary>Receives user input activity only; Girl animations never call this method.</summary>
    public void NotifyActivity(bool keyboard, DateTime at)
    {
        var elapsedFromLastActivity = at > _lastActivityAt ? (at - _lastActivityAt).TotalSeconds : 0;
        var idleSeconds = Math.Max(_secondsSinceActivity, elapsedFromLastActivity);
        if (at > _lastActivityAt) _lastActivityAt = at;
        var wasIdleLongEnough = idleSeconds >= ReactAfterIdleSeconds;
        _secondsSinceActivity = 0;

        if (State is CatState.GoingToSleep or CatState.Sleeping)
        {
            BeginWakingUp();
            return;
        }

        if (State != CatState.Idle || !wasIdleLongEnough) return;
        var chance = keyboard ? 0.35 : 0.15;
        if (_random.NextDouble() < chance) StartTailWag();
    }

    public bool ForceTailWag()
    {
        if (State != CatState.Idle || _tailSequence.Length == 0) return false;
        StartTailWag();
        return true;
    }

    /// <summary>Starts the full sleep transition for the debug menu.</summary>
    public bool ForceSleep()
    {
        if (_sleepFrameCount == 0 || State is not (CatState.Idle or CatState.TailWag)) return false;
        BeginGoingToSleep();
        return true;
    }

    /// <summary>Starts the reverse transition for the debug menu.</summary>
    public bool ForceWake()
    {
        if (State is not (CatState.GoingToSleep or CatState.Sleeping)) return false;
        BeginWakingUp();
        return true;
    }

    public void Advance(TimeSpan elapsed)
    {
        var seconds = Math.Max(0, elapsed.TotalSeconds);
        if (seconds <= 0) return;

        if (State is CatState.Idle or CatState.TailWag)
        {
            _secondsSinceActivity += seconds;

            if (State == CatState.Idle)
            {
                if (_sleepFrameCount > 0 && _secondsSinceActivity >= _sleepTimeoutSeconds)
                {
                    BeginGoingToSleep();
                    return;
                }

                _tailSecondsRemaining -= seconds;
                if (_tailSecondsRemaining <= 0) StartTailWag();
                return;
            }

            AdvanceTailWag(seconds);
            return;
        }

        if (State == CatState.WakingUp) _secondsSinceActivity += seconds;
        _frameElapsed += seconds;
        if (State == CatState.GoingToSleep)
        {
            while (State == CatState.GoingToSleep && _frameElapsed >= SleepFrameSeconds)
            {
                _frameElapsed -= SleepFrameSeconds;
                if (SleepFrameIndex < _sleepFrameCount - 1)
                {
                    SleepFrameIndex++;
                }
                else
                {
                    State = CatState.Sleeping;
                    _frameElapsed = 0;
                    StateChanged?.Invoke(State);
                }
            }
        }
        else if (State == CatState.WakingUp)
        {
            while (State == CatState.WakingUp && _frameElapsed >= WakeFrameSeconds)
            {
                _frameElapsed -= WakeFrameSeconds;
                if (SleepFrameIndex > 0)
                {
                    SleepFrameIndex--;
                }
                else
                {
                    FinishWakingUp();
                }
            }
        }
    }

    private void AdvanceTailWag(double seconds)
    {
        _frameElapsed += seconds;
        while (State == CatState.TailWag && _frameElapsed >= CurrentTailFrameDuration())
        {
            _frameElapsed -= CurrentTailFrameDuration();
            _tailSequenceIndex++;
            if (_tailSequenceIndex >= _tailSequence.Length)
            {
                TailFrameIndex = -1;
                _frameElapsed = 0;
                if (_sleepFrameCount > 0 && _secondsSinceActivity >= _sleepTimeoutSeconds)
                {
                    BeginGoingToSleep();
                }
                else
                {
                    State = CatState.Idle;
                    ScheduleNextTailWag();
                    StateChanged?.Invoke(State);
                }
                break;
            }

            TailFrameIndex = _tailSequence[_tailSequenceIndex];
        }
    }

    private double CurrentTailFrameDuration() => _tailSequence[_tailSequenceIndex] == _tailFrameCount - 1 ? 0.14 : 0.16;

    private void StartTailWag()
    {
        if (State != CatState.Idle || _tailSequence.Length == 0) return;
        State = CatState.TailWag;
        _tailSequenceIndex = 0;
        _frameElapsed = 0;
        TailFrameIndex = _tailSequence[0];
        StateChanged?.Invoke(State);
    }

    private void BeginGoingToSleep()
    {
        State = CatState.GoingToSleep;
        TailFrameIndex = -1;
        SleepFrameIndex = 0;
        _frameElapsed = 0;
        StateChanged?.Invoke(State);
    }

    private void BeginWakingUp()
    {
        State = CatState.WakingUp;
        TailFrameIndex = -1;
        if (SleepFrameIndex < 0) SleepFrameIndex = 0;
        _frameElapsed = 0;
        StateChanged?.Invoke(State);
    }

    private void FinishWakingUp()
    {
        State = CatState.Idle;
        SleepFrameIndex = -1;
        _frameElapsed = 0;
        _tailSecondsRemaining = _tailFrameCount == 0 ? double.PositiveInfinity :
            _random.NextDouble() < 0.35 ? _random.Next(1, 3) : _random.Next(8, 21);
        StateChanged?.Invoke(State);
    }

    private void ScheduleNextTailWag() => _tailSecondsRemaining = _tailSequence.Length == 0 ? double.PositiveInfinity : _random.Next(8, 21);
}
