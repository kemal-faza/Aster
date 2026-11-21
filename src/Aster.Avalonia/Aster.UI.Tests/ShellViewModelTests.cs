using Aster.UI.Services;
using Aster.UI.ViewModels;
using Aster.Core.Models;

namespace Aster.UI.Tests;

public class ShellViewModelTests
{
    [Fact]
    public void ClockUsesCurrentCultureFormat()
    {
        var vm = new ShellViewModel();
        vm.UpdateClock(new DateTimeOffset(2026, 11, 14, 14, 5, 0, TimeSpan.Zero));

        Assert.Equal("14:05", vm.ClockText);
    }

    [Fact]
    public void DateUsesCurrentCulture()
    {
        var vm = new ShellViewModel();
        vm.UpdateClock(new DateTimeOffset(2026, 11, 14, 14, 5, 0, TimeSpan.Zero));

        Assert.False(string.IsNullOrWhiteSpace(vm.DateText));
    }

    [Fact]
    public void OpacityTracksExpansionProgress()
    {
        var vm = new ShellViewModel();

        vm.ExpansionProgress = 0;
        Assert.Equal(1, vm.CollapsedOpacity);
        Assert.Equal(0, vm.ExpandedOpacity);

        vm.ExpansionProgress = 1;
        Assert.Equal(0, vm.CollapsedOpacity);
        Assert.Equal(1, vm.ExpandedOpacity);
    }

    [Fact]
    public void PanelModeDefaultsToMusicAndCanChange()
    {
        var vm = new ShellViewModel();
        Assert.Equal(PanelMode.Music, vm.PanelMode);
        vm.PanelMode = PanelMode.Focus;
        Assert.Equal(PanelMode.Focus, vm.PanelMode);
    }

    [Fact]
    public void ApplySettingsUpdatesMinimalAndGlass()
    {
        var vm = new ShellViewModel();

        vm.ApplySettings(new AppSettings(
            SchemaVersion: SettingsSchema.CurrentVersion,
            StartWithWindows: false,
            MinimalNotch: true,
            HideInFullscreen: true,
            GlassBackground: false,
            BouncySpring: true,
            ShowMascot: true,
            VolumeToast: true,
            BrightnessToast: true,
            LyricsEnabled: true,
            WaterReminderEnabled: false,
            WaterIntervalMinutes: 60,
            WaterActiveStartHour: 8,
            WaterActiveEndHour: 22,
            WaterDailyGoal: 8,
            WaterGlassesToday: 0,
            WaterDate: string.Empty,
            CameraMirror: true,
            FocusMinutes: 25,
            ShortBreakMinutes: 5,
            LongBreakMinutes: 15,
            FocusChime: true));

        Assert.True(vm.IsMinimal);
        Assert.False(vm.GlassBackground);
    }

    [Fact]
    public void FocusPillWinsOverClockAndMedia()
    {
        var vm = new ShellViewModel { Focus = TestFocus.Create(showInPill: true) };

        Assert.True(vm.ShowFocusPill);
        Assert.False(vm.ShowClockRow);
        Assert.Equal("25:00", vm.PillText);
    }

    [Fact]
    public void ExpandingWithASessionSwitchesToFocus()
    {
        var vm = new ShellViewModel { Focus = TestFocus.Create(showInPill: true) };

        vm.IsExpanded = true;

        Assert.Equal(PanelMode.Focus, vm.PanelMode);
    }

    [Fact]
    public void StartingASessionSwitchesToFocus()
    {
        var (focus, service, _, _) = TestFocus.CreateWithService();
        var shell = new ShellViewModel { Focus = focus };

        service.StartSession();

        Assert.Equal(PanelMode.Focus, shell.PanelMode);
    }

    [Fact]
    public void CompletingASessionSwitchesToFocus()
    {
        var (focus, service, time, timer) = TestFocus.CreateWithService();
        var shell = new ShellViewModel { Focus = focus };
        service.StartSession();
        shell.PanelMode = PanelMode.Music;

        time.Advance(TimeSpan.FromMinutes(26));
        timer.Fire();

        Assert.Equal(PanelMode.Focus, shell.PanelMode);
    }

    [Fact]
    public void ShelfCapabilityIsEnabledByDefault()
    {
        var vm = new ShellViewModel();

        Assert.True(vm.HasShelf);
    }

    [Fact]
    public void ShowShelfPanelTracksModeAndCapability()
    {
        var vm = new ShellViewModel();
        Assert.False(vm.ShowShelfPanel);

        vm.PanelMode = PanelMode.Shelf;

        Assert.True(vm.IsShelfMode);
        Assert.True(vm.ShowShelfPanel);

        var unsupported = new ShellViewModel(
            capabilities: new CapabilityMatrix(Capability.None));
        unsupported.PanelMode = PanelMode.Shelf;

        Assert.False(unsupported.HasShelf);
        Assert.False(unsupported.ShowShelfPanel);
    }

    [Fact]
    public void ShelfActivityFollowsExpansionAndMode()
    {
        var timer = new ManualUiTimer();
        var shelf = new ShelfViewModel(
            new Aster.Core.Shelf.ShelfService(), null, timer, new NullLog());
        var vm = new ShellViewModel { Shelf = shelf };

        vm.IsExpanded = true;
        Assert.False(timer.IsEnabled);

        vm.PanelMode = PanelMode.Shelf;
        Assert.True(timer.IsEnabled);

        vm.PanelMode = PanelMode.Music;
        Assert.False(timer.IsEnabled);

        vm.PanelMode = PanelMode.Shelf;
        vm.IsExpanded = false;
        Assert.False(timer.IsEnabled);
    }

    private sealed class NullLog : Aster.Core.Interfaces.IAppLog
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }
}

internal static class TestFocus
{
    public static FocusViewModel Create(bool showInPill = false) =>
        CreateWithService(showInPill).Vm;

    public static (FocusViewModel Vm, FocusService Service,
        ManualTimeProvider Time, ManualUiTimer Timer)
        CreateWithService(bool showInPill = false)
    {
        var store = new TestSettingsStore(SettingsSchema.Default);
        var time = new ManualTimeProvider(
            new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.Zero));
        var timer = new ManualUiTimer();
        var service = new FocusService(store, time, timer, new NullLog());
        var vm = new FocusViewModel(
            service,
            store,
            new ToastViewModel(new ManualUiTimer()),
            null,
            new NullLog());

        if (showInPill)
        {
            service.StartSession();
        }

        return (vm, service, time, timer);
    }

    private sealed class NullLog : Aster.Core.Interfaces.IAppLog
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }
}
