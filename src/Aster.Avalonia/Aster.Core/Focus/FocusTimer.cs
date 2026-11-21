namespace Aster.Core.Focus;

public readonly record struct FocusAdvanceResult(
    bool Ticked,
    bool DayChanged,
    bool Completed,
    FocusPhase Phase);

public sealed class FocusTimer
{
    private readonly TimeProvider _time;
    private FocusDurations _durations;
    private FocusPhase _phase = FocusPhase.Focus;
    private TimeSpan _duration;
    private TimeSpan _remaining;
    private bool _running;
    private bool _sessionInProgress;
    private bool _complete;
    private long _startedAt;
    private TimeSpan _startRemaining;
    private DateOnly _sessionDate;

    public FocusTimer(TimeProvider time, FocusDurations durations)
    {
        _time = time;
        _durations = durations;
        _duration = durations.For(_phase);
        _remaining = _duration;
        _sessionDate = Today();
    }

    public FocusPhase Phase => _phase;
    public TimeSpan Duration => _duration;
    public TimeSpan Remaining => _remaining;
    public bool IsRunning => _running;
    public bool IsComplete => _complete;
    public bool IsPaused => _sessionInProgress && !_running;
    public bool ShowInPill => _sessionInProgress;
    public bool HasSession => _sessionInProgress || _complete;
    public int CompletedSessionsToday { get; private set; }

    public void Start()
    {
        RefreshDay();
        if (_running)
        {
            return;
        }

        if (_complete)
        {
            _complete = false;
            _duration = _durations.For(_phase);
            _remaining = _duration;
        }

        if (_remaining <= TimeSpan.Zero)
        {
            _remaining = _duration;
        }

        _startedAt = _time.GetTimestamp();
        _startRemaining = _remaining;
        _running = true;
        _sessionInProgress = true;
    }

    public void Pause()
    {
        if (!_running)
        {
            return;
        }

        _remaining = CurrentRemaining();
        if (_remaining < TimeSpan.Zero)
        {
            _remaining = TimeSpan.Zero;
        }

        _running = false;
    }

    public void Toggle()
    {
        if (_running)
        {
            Pause();
        }
        else
        {
            Start();
        }
    }

    public void Reset()
    {
        _running = false;
        _complete = false;
        _sessionInProgress = false;
        _duration = _durations.For(_phase);
        _remaining = _duration;
    }

    public void SetPhase(FocusPhase phase)
    {
        _phase = phase;
        Reset();
    }

    public FocusPhase SuggestBreakPhase() =>
        CompletedSessionsToday > 0 && CompletedSessionsToday % 4 == 0
            ? FocusPhase.LongBreak
            : FocusPhase.ShortBreak;

    public void StartSuggestedNext()
    {
        if (_complete && _phase != FocusPhase.Focus)
        {
            SetPhase(FocusPhase.Focus);
            Start();
            return;
        }

        SetPhase(SuggestBreakPhase());
        Start();
    }

    public FocusAdvanceResult Advance()
    {
        var dayChanged = RefreshDay();
        if (!_running)
        {
            return new FocusAdvanceResult(false, dayChanged, false, _phase);
        }

        var remaining = CurrentRemaining();
        if (remaining > TimeSpan.Zero)
        {
            _remaining = remaining;
            return new FocusAdvanceResult(true, dayChanged, false, _phase);
        }

        _remaining = TimeSpan.Zero;
        _running = false;
        _complete = true;
        _sessionInProgress = false;
        if (_phase == FocusPhase.Focus)
        {
            CompletedSessionsToday++;
        }

        return new FocusAdvanceResult(true, dayChanged, true, _phase);
    }

    public void UpdateDurations(FocusDurations durations)
    {
        _durations = durations;
        if (!_running && !_sessionInProgress && !_complete)
        {
            _duration = durations.For(_phase);
            _remaining = _duration;
        }
    }

    private TimeSpan CurrentRemaining() =>
        _startRemaining - _time.GetElapsedTime(_startedAt);

    private bool RefreshDay()
    {
        var today = Today();
        if (today == _sessionDate)
        {
            return false;
        }

        _sessionDate = today;
        CompletedSessionsToday = 0;
        return true;
    }

    private DateOnly Today() =>
        DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
}
