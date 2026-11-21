using Aster.Core.Models;
using Aster.UI.Services;
using Aster.UI.ViewModels;

namespace Aster.UI.Tests;

public class WaterViewModelTests
{
    private static readonly DateTimeOffset StartUtc =
        new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    private static (WaterViewModel Vm, WaterService Service,
        TestSettingsStore Store, ManualTimeProvider Time,
        ToastViewModel Toast) Create(AppSettings? settings = null)
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
        var service = new WaterService(store, time, new ManualUiTimer(), new NullLog());
        var toast = new ToastViewModel(new ManualUiTimer());
        var vm = new WaterViewModel(service, toast, new NullLog());
        return (vm, service, store, time, toast);
    }

    [Fact]
    public async Task CountAndBarsFollowTheStore()
    {
        var (vm, service, store, _, _) = Create();
        store.Raise(store.Current with { WaterGlassesToday = 4 });
        await service.TickAsync();

        Assert.Equal(4, vm.Count);
        Assert.Equal("4/8", vm.CountText);
        Assert.Equal(4, vm.Bars.Count(b => b.Filled));
        Assert.All(vm.Bars.Where(b => b.Filled), b => Assert.False(b.Goal));
    }

    [Fact]
    public void GoalBarsAreGreenAtTheGoal()
    {
        var (vm, _, store, _, _) = Create();
        store.Raise(store.Current with { WaterGlassesToday = 8 });

        Assert.True(vm.IsGoalReached);
        Assert.All(vm.Bars, b => Assert.True(b.Filled && b.Goal));
        Assert.Equal("8/8", vm.CountText);
        Assert.Contains("goal reached", vm.StatusText);
    }

    [Fact]
    public void StatusPriorityIsRemindersOffThenGoalThenNext()
    {
        var (vm, _, store, _, _) = Create();
        store.Raise(store.Current with { WaterReminderEnabled = false });
        Assert.Contains("reminders off", vm.StatusText);

        store.Raise(store.Current with
        {
            WaterReminderEnabled = true,
            WaterGlassesToday = 8
        });
        Assert.Contains("goal reached", vm.StatusText);
    }

    [Fact]
    public void TooltipTracksCountAndGoal()
    {
        var (vm, _, store, _, _) = Create();
        Assert.Equal("0 of 8 glasses today · click to log", vm.Tooltip);

        store.Raise(store.Current with { WaterGlassesToday = 8 });
        Assert.Equal("Goal reached · 8 glasses today", vm.Tooltip);
    }

    [Fact]
    public async Task ReminderToastIsClickableAndLogs()
    {
        var (vm, service, store, time, toast) = Create();
        await service.InitializeAsync();
        var reminders = 0;
        service.ReminderDue += (_, _) => reminders++;
        time.Advance(TimeSpan.FromHours(1));
        await service.TickAsync();

        Assert.Equal(1, reminders);
        Assert.True(toast.IsClickable);
        Assert.Equal("Time to drink · 0/8", toast.Text);

        toast.Invoke();

        Assert.Equal(1, store.Current.WaterGlassesToday);
        Assert.Equal("Glass logged · 1/8", toast.Text);
    }

    [Fact]
    public void GoalToastHasNoClick()
    {
        var (_, service, store, _, toast) = Create();
        store.Raise(store.Current with { WaterGlassesToday = 8 });
        service.Preview();

        Assert.False(toast.IsClickable);
        Assert.Equal("Goal reached · 8/8", toast.Text);
    }

    [Fact]
    public void PreviewShowsReminderWhenDisabled()
    {
        var (vm, service, store, _, toast) = Create();
        store.Raise(store.Current with { WaterReminderEnabled = false });

        vm.PreviewCommand.Execute(null);

        Assert.True(toast.IsVisible);
        Assert.Equal("Time to drink · 0/8", toast.Text);
    }

    private sealed class NullLog : Aster.Core.Interfaces.IAppLog
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }
}
