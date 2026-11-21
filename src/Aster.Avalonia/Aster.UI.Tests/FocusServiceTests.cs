using Aster.Core.Focus;
using Aster.Core.Models;
using Aster.UI.Services;

namespace Aster.UI.Tests;

public class FocusServiceTests
{
    private static readonly DateTimeOffset StartUtc =
        new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    private static (FocusService Service, ManualTimeProvider Time, ManualUiTimer Timer) Create()
    {
        var store = new TestSettingsStore(SettingsSchema.Default with
        {
            FocusMinutes = 1,
            ShortBreakMinutes = 1,
            LongBreakMinutes = 1
        });
        var time = new ManualTimeProvider(StartUtc);
        var timer = new ManualUiTimer();
        var service = new FocusService(store, time, timer, new NullLog());
        return (service, time, timer);
    }

    [Fact]
    public void StartRunsTheUiTimerAndRaisesState()
    {
        var (service, _, timer) = Create();
        var states = 0;
        service.StateChanged += (_, _) => states++;

        service.StartSession();

        Assert.True(service.Timer.IsRunning);
        Assert.True(timer.IsEnabled);
        Assert.Equal(1, states);
    }

    [Fact]
    public void CompletedStopsTheTimerAndRaisesCompleted()
    {
        var (service, time, timer) = Create();
        FocusPhase? completed = null;
        service.Completed += (_, phase) => completed = phase;

        service.StartSession();
        time.Advance(TimeSpan.FromMinutes(2));
        timer.Fire();

        Assert.Equal(FocusPhase.Focus, completed);
        Assert.True(service.Timer.IsComplete);
        Assert.False(timer.IsEnabled);
    }

    [Fact]
    public void DayChangeRaisesStateWithoutCompletion()
    {
        var store = new TestSettingsStore(SettingsSchema.Default with
        {
            FocusMinutes = 25,
            ShortBreakMinutes = 5,
            LongBreakMinutes = 15
        });
        var time = new ManualTimeProvider(
            new DateTimeOffset(2026, 11, 20, 23, 59, 0, TimeSpan.Zero));
        var timer = new ManualUiTimer();
        var service = new FocusService(store, time, timer, new NullLog());
        var states = 0;
        service.StateChanged += (_, _) => states++;

        service.StartSession();
        time.Advance(TimeSpan.FromMinutes(1));
        timer.Fire();

        Assert.True(states >= 2);
        Assert.False(service.Timer.IsComplete);
    }

    [Fact]
    public void ChangingFocusDurationsDoesNotRestartARunningSession()
    {
        var store = new TestSettingsStore(SettingsSchema.Default with
        {
            FocusMinutes = 25,
            ShortBreakMinutes = 5,
            LongBreakMinutes = 15
        });
        var time = new ManualTimeProvider(StartUtc);
        var timer = new ManualUiTimer();
        var service = new FocusService(store, time, timer, new NullLog());
        service.StartSession();

        store.Raise(store.Current with { FocusMinutes = 40 });

        Assert.Equal(TimeSpan.FromMinutes(25), service.Timer.Duration);
    }

    [Fact]
    public void WaterOnlySettingsChangesDoNotRaiseState()
    {
        var store = new TestSettingsStore(SettingsSchema.Default);
        var time = new ManualTimeProvider(StartUtc);
        var timer = new ManualUiTimer();
        var service = new FocusService(store, time, timer, new NullLog());
        var states = 0;
        service.StateChanged += (_, _) => states++;

        store.Raise(store.Current with { WaterGlassesToday = 1 });

        Assert.Equal(0, states);
    }

    [Fact]
    public void PauseAndResetStopTheUiTimer()
    {
        var (service, _, timer) = Create();

        service.StartSession();
        service.PauseSession();
        Assert.False(timer.IsEnabled);

        service.StartSession();
        service.ResetSession();
        Assert.False(timer.IsEnabled);
    }

    private sealed class NullLog : Aster.Core.Interfaces.IAppLog
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }
}
