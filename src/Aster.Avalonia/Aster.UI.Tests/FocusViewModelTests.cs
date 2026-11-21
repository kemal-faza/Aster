using Aster.Core.Models;
using Aster.UI.Controls;
using Aster.UI.Services;
using Aster.UI.ViewModels;

namespace Aster.UI.Tests;

public class FocusViewModelTests
{
    private static readonly DateTimeOffset StartUtc =
        new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    private static (FocusViewModel Vm, FocusService Service,
        ManualTimeProvider Time, ManualUiTimer Timer,
        TestSettingsStore Store, ToastViewModel Toast, FakeChime Chime) Create()
    {
        var store = new TestSettingsStore(SettingsSchema.Default with
        {
            FocusMinutes = 1,
            ShortBreakMinutes = 1,
            LongBreakMinutes = 1,
            FocusChime = true
        });
        var time = new ManualTimeProvider(StartUtc);
        var timer = new ManualUiTimer();
        var service = new FocusService(store, time, timer, new NullLog());
        var toast = new ToastViewModel(new ManualUiTimer());
        var chime = new FakeChime();
        var vm = new FocusViewModel(service, store, toast, chime, new NullLog());
        return (vm, service, time, timer, store, toast, chime);
    }

    [Fact]
    public void InitialLabelsMatchWpf()
    {
        var (vm, _, _, _, _, _, _) = Create();

        Assert.Equal("1:00", vm.CountdownText);
        Assert.Equal("Focus 1", vm.FocusChipText);
        Assert.Equal("Short 1", vm.ShortChipText);
        Assert.Equal("Long 1", vm.LongChipText);
        Assert.Equal("Focus session", vm.Title);
        Assert.True(vm.IsFocusPhase);
        Assert.Equal("Start", vm.StartLabel);
        Assert.True(vm.IsStartVisible);
        Assert.False(vm.IsBreakVisible);
    }

    [Fact]
    public void StartUpdatesCountdownAndPauseLabel()
    {
        var (vm, service, time, timer, _, _, _) = Create();

        service.StartSession();
        time.Advance(TimeSpan.FromSeconds(30));
        timer.Fire();

        Assert.Equal("0:30", vm.CountdownText);
        Assert.Equal("0:30", vm.RemainingText);
        Assert.Equal("Pause", vm.StartLabel);
        Assert.True(vm.IsRunning);
    }

    [Fact]
    public void CompletionShowsBreakCopyToastAndChime()
    {
        var (vm, service, time, timer, _, toast, chime) = Create();
        service.StartSession();
        time.Advance(TimeSpan.FromMinutes(2));
        timer.Fire();

        Assert.True(vm.IsComplete);
        Assert.False(vm.IsStartVisible);
        Assert.True(vm.IsBreakVisible);
        Assert.Equal("Start short break", vm.BreakLabel);
        Assert.Equal(Glyph.Get(Glyph.Kind.Focus), toast.Glyph);
        Assert.Equal("Focus complete · Take a break", toast.Text);
        Assert.Equal(1, chime.Plays);
    }

    [Fact]
    public void ChimeDisabledDoesNotPlay()
    {
        var (vm, service, time, timer, store, _, chime) = Create();
        store.Raise(store.Current with { FocusChime = false });

        service.StartSession();
        time.Advance(TimeSpan.FromMinutes(2));
        timer.Fire();

        Assert.Equal(0, chime.Plays);
    }

    [Fact]
    public void ChimeFailureLogsOnce()
    {
        var store = new TestSettingsStore(SettingsSchema.Default with
        {
            FocusMinutes = 1,
            ShortBreakMinutes = 1,
            LongBreakMinutes = 1,
            FocusChime = true
        });
        var time = new ManualTimeProvider(StartUtc);
        var timer = new ManualUiTimer();
        var service = new FocusService(store, time, timer, new NullLog());
        var log = new RecordingLog();
        var vm = new FocusViewModel(
            service,
            store,
            new ToastViewModel(new ManualUiTimer()),
            new ThrowingChime(),
            log);

        service.StartSession();
        time.Advance(TimeSpan.FromMinutes(2));
        timer.Fire();
        service.StartSuggestedNext();
        time.Advance(TimeSpan.FromMinutes(2));
        timer.Fire();

        Assert.Single(log.Warnings);
    }

    [Fact]
    public void BreakCompletionUsesBreakCopy()
    {
        var (vm, service, time, timer, _, toast, _) = Create();
        service.SelectPhase(Aster.Core.Focus.FocusPhase.ShortBreak);
        service.StartSession();
        time.Advance(TimeSpan.FromMinutes(2));
        timer.Fire();

        Assert.Equal("Start focus", vm.BreakLabel);
        Assert.Equal("Break over · Back to focus", toast.Text);
    }

    [Fact]
    public void SelectionCommandsChangePhase()
    {
        var (vm, _, _, _, _, _, _) = Create();

        vm.SelectShortCommand.Execute(null);
        Assert.True(vm.IsShortBreakPhase);
        Assert.Equal("Short break", vm.Title);

        vm.SelectLongCommand.Execute(null);
        Assert.True(vm.IsLongBreakPhase);
        Assert.Equal("Long break", vm.Title);

        vm.SelectFocusCommand.Execute(null);
        Assert.True(vm.IsFocusPhase);
        Assert.Equal("Focus session", vm.Title);
    }

    private sealed class FakeChime : Aster.UI.Abstractions.IChimePlayer
    {
        public int Plays { get; private set; }
        public void Play() => Plays++;
    }

    private sealed class ThrowingChime : Aster.UI.Abstractions.IChimePlayer
    {
        public void Play() => throw new InvalidOperationException("no player");
    }

    private sealed class RecordingLog : Aster.Core.Interfaces.IAppLog
    {
        public List<string> Warnings { get; } = new();
        public void Info(string message) { }
        public void Warn(string message) => Warnings.Add(message);
        public void Error(string message, Exception? exception = null) { }
    }

    private sealed class NullLog : Aster.Core.Interfaces.IAppLog
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }
}
