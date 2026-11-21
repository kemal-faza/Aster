using Aster.Core.Focus;

namespace Aster.Core.Tests;

public class FocusTimerTests
{
    private static readonly DateTimeOffset StartUtc =
        new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    private static (FocusTimer Timer, ManualTimeProvider Time) Create(
        int focus = 25, int shortBreak = 5, int longBreak = 15)
    {
        var time = new ManualTimeProvider(StartUtc);
        var timer = new FocusTimer(
            time,
            FocusDurations.FromMinutes(focus, shortBreak, longBreak));
        return (timer, time);
    }

    [Fact]
    public void StartsIdleAtTheFocusDuration()
    {
        var (timer, _) = Create();

        Assert.Equal(FocusPhase.Focus, timer.Phase);
        Assert.Equal(TimeSpan.FromMinutes(25), timer.Duration);
        Assert.Equal(TimeSpan.FromMinutes(25), timer.Remaining);
        Assert.False(timer.IsRunning);
        Assert.False(timer.ShowInPill);
        Assert.False(timer.HasSession);
    }

    [Fact]
    public void StartAndAdvanceUseMonotonicTime()
    {
        var (timer, time) = Create();

        timer.Start();
        time.Advance(TimeSpan.FromSeconds(90));
        var result = timer.Advance();

        Assert.True(timer.IsRunning);
        Assert.True(timer.ShowInPill);
        Assert.True(result.Ticked);
        Assert.False(result.Completed);
        Assert.Equal(TimeSpan.FromMinutes(25) - TimeSpan.FromSeconds(90), timer.Remaining);
    }

    [Fact]
    public void DoubleStartDoesNotMoveTheEndTime()
    {
        var (timer, time) = Create();

        timer.Start();
        time.Advance(TimeSpan.FromSeconds(30));
        timer.Start();
        time.Advance(TimeSpan.FromSeconds(30));
        timer.Advance();

        Assert.Equal(TimeSpan.FromMinutes(25) - TimeSpan.FromSeconds(60), timer.Remaining);
    }

    [Fact]
    public void PauseFreezesRemainingAndResumeContinues()
    {
        var (timer, time) = Create();

        timer.Start();
        time.Advance(TimeSpan.FromSeconds(60));
        timer.Pause();
        time.Advance(TimeSpan.FromMinutes(10));

        Assert.True(timer.IsPaused);
        Assert.Equal(TimeSpan.FromMinutes(24), timer.Remaining);

        timer.Start();
        time.Advance(TimeSpan.FromSeconds(60));
        timer.Advance();

        Assert.Equal(TimeSpan.FromMinutes(23), timer.Remaining);
    }

    [Fact]
    public void CompletionIncrementsOnlyFocusSessions()
    {
        var (timer, time) = Create(focus: 1);

        timer.Start();
        time.Advance(TimeSpan.FromMinutes(2));
        var result = timer.Advance();

        Assert.True(result.Completed);
        Assert.True(timer.IsComplete);
        Assert.False(timer.ShowInPill);
        Assert.True(timer.HasSession);
        Assert.Equal(1, timer.CompletedSessionsToday);

        timer.SetPhase(FocusPhase.ShortBreak);
        timer.Start();
        time.Advance(TimeSpan.FromMinutes(10));
        timer.Advance();

        Assert.Equal(1, timer.CompletedSessionsToday);
    }

    [Fact]
    public void LongBreakIsSuggestedEveryFourFocusSessions()
    {
        var (timer, time) = Create(focus: 1, shortBreak: 1, longBreak: 1);

        for (var i = 0; i < 4; i++)
        {
            timer.Start();
            time.Advance(TimeSpan.FromMinutes(2));
            timer.Advance();
        }

        Assert.Equal(4, timer.CompletedSessionsToday);
        Assert.Equal(FocusPhase.LongBreak, timer.SuggestBreakPhase());
    }

    [Fact]
    public void StartSuggestedNextAlternatesFocusAndBreak()
    {
        var (timer, time) = Create(focus: 1, shortBreak: 1);

        timer.Start();
        time.Advance(TimeSpan.FromMinutes(2));
        timer.Advance();
        timer.StartSuggestedNext();

        Assert.Equal(FocusPhase.ShortBreak, timer.Phase);
        Assert.True(timer.IsRunning);

        time.Advance(TimeSpan.FromMinutes(2));
        timer.Advance();
        timer.StartSuggestedNext();

        Assert.Equal(FocusPhase.Focus, timer.Phase);
        Assert.True(timer.IsRunning);
    }

    [Fact]
    public void DurationChangesApplyOnlyWhenIdle()
    {
        var (timer, time) = Create();

        timer.UpdateDurations(FocusDurations.FromMinutes(30, 6, 16));
        Assert.Equal(TimeSpan.FromMinutes(30), timer.Duration);

        timer.Start();
        timer.UpdateDurations(FocusDurations.FromMinutes(40, 7, 17));
        Assert.Equal(TimeSpan.FromMinutes(30), timer.Duration);

        time.Advance(TimeSpan.FromMinutes(31));
        timer.Advance();
        timer.Start();

        Assert.Equal(TimeSpan.FromMinutes(40), timer.Duration);
    }

    [Fact]
    public void MidnightRolloverResetsThenCountsTheCompletingSession()
    {
        var time = new ManualTimeProvider(
            new DateTimeOffset(2026, 11, 20, 23, 59, 0, TimeSpan.Zero));
        var timer = new FocusTimer(time, FocusDurations.FromMinutes(1, 1, 1));

        timer.Start();
        time.Advance(TimeSpan.FromMinutes(2));
        var result = timer.Advance();

        Assert.True(result.DayChanged);
        Assert.True(result.Completed);
        Assert.Equal(1, timer.CompletedSessionsToday);
    }

    [Fact]
    public void MidnightRolloverWithoutCompletionResetsTheSessionCount()
    {
        var time = new ManualTimeProvider(
            new DateTimeOffset(2026, 11, 20, 23, 59, 0, TimeSpan.Zero));
        var timer = new FocusTimer(time, FocusDurations.FromMinutes(25, 5, 15));

        timer.Start();
        time.Advance(TimeSpan.FromMinutes(1));
        var result = timer.Advance();

        Assert.True(result.DayChanged);
        Assert.False(result.Completed);
        Assert.Equal(0, timer.CompletedSessionsToday);
    }

    [Fact]
    public void PausedAtZeroCanRestart()
    {
        var (timer, time) = Create(focus: 1);

        timer.Start();
        time.Advance(TimeSpan.FromMinutes(1));
        timer.Pause();
        Assert.Equal(TimeSpan.Zero, timer.Remaining);

        timer.Start();

        Assert.True(timer.IsRunning);
        Assert.Equal(TimeSpan.FromMinutes(1), timer.Remaining);
    }
}
