using Aster.Core.Models;
using Aster.Core.Water;
using Aster.UI.Services;

namespace Aster.UI.Tests;

public class WaterServiceTests
{
    private static readonly DateTimeOffset StartUtc =
        new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    private static (WaterService Service, TestSettingsStore Store,
        ManualTimeProvider Time, ManualUiTimer Timer) Create(AppSettings? settings = null)
    {
        var store = new TestSettingsStore(settings ?? SettingsSchema.Default with
        {
            WaterReminderEnabled = true,
            WaterIntervalMinutes = 60,
            WaterActiveStartHour = 8,
            WaterActiveEndHour = 22,
            WaterDailyGoal = 8,
            WaterDate = "2026-11-20"
        });
        var time = new ManualTimeProvider(StartUtc);
        var timer = new ManualUiTimer();
        var service = new WaterService(store, time, timer, new NullLog());
        return (service, store, time, timer);
    }

    [Fact]
    public async Task InitializeComputesNextReminder()
    {
        var (service, _, _, _) = Create();

        await service.InitializeAsync();

        Assert.Equal(StartUtc.AddHours(1), service.NextReminderAt);
    }

    [Fact]
    public async Task RolloverResetsTheCountAndRaisesState()
    {
        var (service, store, time, _) = Create();
        store.Raise(store.Current with { WaterGlassesToday = 5 });
        var states = 0;
        service.StateChanged += (_, _) => states++;

        time.Advance(TimeSpan.FromDays(1));
        await service.TickAsync();

        Assert.Equal(0, service.GlassesToday);
        Assert.True(states >= 1);
    }

    [Fact]
    public async Task RolloverRaisesStateWhenDisabledAndAtGoal()
    {
        var (service, store, time, _) = Create();
        store.Raise(store.Current with
        {
            WaterReminderEnabled = false,
            WaterGlassesToday = 8
        });
        var states = 0;
        service.StateChanged += (_, _) => states++;

        time.Advance(TimeSpan.FromDays(1));
        await service.TickAsync();

        Assert.Equal(0, service.GlassesToday);
        Assert.True(states >= 1);
    }

    [Fact]
    public async Task OverdueReminderFiresAfterUnsuppress()
    {
        var (service, _, time, _) = Create();
        await service.InitializeAsync();
        var reminders = 0;
        service.ReminderDue += (_, _) => reminders++;

        service.IsSuppressed = true;
        time.Advance(TimeSpan.FromHours(2));
        service.IsSuppressed = false;
        await service.TickAsync();

        Assert.Equal(1, reminders);
    }

    [Fact]
    public async Task OutsideActiveHoursSchedulesTheNextStart()
    {
        var (service, _, time, _) = Create();
        await service.InitializeAsync();
        time.Advance(TimeSpan.FromHours(14));

        await service.TickAsync();

        Assert.Equal(
            new DateTimeOffset(2026, 11, 21, 9, 0, 0, TimeSpan.Zero),
            service.NextReminderAt);
    }

    [Fact]
    public async Task LogDrinkIncrementsAndPersists()
    {
        var (service, store, _, _) = Create();
        await service.InitializeAsync();

        var result = await service.LogDrinkAsync();

        Assert.Equal(WaterDrinkResult.Logged, result);
        Assert.Equal(1, store.Current.WaterGlassesToday);
        Assert.Equal("2026-11-20", store.Current.WaterDate);
    }

    [Fact]
    public async Task TwoRapidLogsDoNotLoseAGlass()
    {
        var (service, store, _, _) = Create();
        await service.InitializeAsync();

        await Task.WhenAll(service.LogDrinkAsync(), service.LogDrinkAsync());

        Assert.Equal(2, store.Current.WaterGlassesToday);
    }

    [Fact]
    public async Task LogDrinkAtGoalReturnsGoalReached()
    {
        var (service, store, _, _) = Create();
        store.Raise(store.Current with { WaterGlassesToday = 8 });

        var result = await service.LogDrinkAsync();

        Assert.Equal(WaterDrinkResult.GoalReached, result);
        Assert.Equal(8, store.Current.WaterGlassesToday);
    }

    [Fact]
    public async Task FailedUpdateReturnsFailedAndDoesNotIncrement()
    {
        var (service, store, _, _) = Create();
        store.FailUpdates = true;

        var result = await service.LogDrinkAsync();

        Assert.Equal(WaterDrinkResult.Failed, result);
        Assert.Equal(0, store.Current.WaterGlassesToday);
    }

    [Fact]
    public async Task ResetTodaySetsZero()
    {
        var (service, store, _, _) = Create();
        store.Raise(store.Current with { WaterGlassesToday = 5 });

        await service.ResetTodayAsync();

        Assert.Equal(0, store.Current.WaterGlassesToday);
    }

    [Fact]
    public async Task DisposeStopsTheTimer()
    {
        var (service, _, _, timer) = Create();
        service.Start();

        await service.DisposeAsync();

        Assert.False(timer.IsEnabled);
    }

    [Fact]
    public void PreviewWorksWhenDisabled()
    {
        var (service, store, _, _) = Create();
        store.Raise(store.Current with { WaterReminderEnabled = false });
        var reminders = 0;
        service.ReminderDue += (_, _) => reminders++;

        service.Preview();

        Assert.Equal(1, reminders);
    }

    private sealed class NullLog : Aster.Core.Interfaces.IAppLog
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }
}
