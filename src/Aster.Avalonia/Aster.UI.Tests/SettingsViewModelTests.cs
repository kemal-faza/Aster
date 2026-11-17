using Aster.UI.ViewModels;
using Aster.Core.Interfaces;
using Aster.Core.Models;

namespace Aster.UI.Tests;

public class SettingsViewModelTests
{
    [Fact]
    public async Task ToggleSavesAllFields()
    {
        var store = new FakeStore();
        var vm = new SettingsViewModel(store, new NullLog(), "Start with Windows")
        {
            StartWithWindows = true,
            MinimalNotch = true,
            HideInFullscreen = false,
            GlassBackground = false,
            BouncySpring = false
        };

        await vm.SaveNowAsync();

        Assert.NotNull(store.Saved);
        Assert.True(store.Saved!.StartWithWindows);
        Assert.True(store.Saved.MinimalNotch);
        Assert.False(store.Saved.HideInFullscreen);
        Assert.False(store.Saved.GlassBackground);
        Assert.False(store.Saved.BouncySpring);
    }

    [Fact]
    public async Task UneditedFieldsSurviveSave()
    {
        var seeded = SettingsSchema.Default with
        {
            ShowMascot = false,
            LyricsHighlightColor = "#112233",
            WaterDailyGoal = 12,
            WaterDate = "2026-11-17",
            CameraMirror = false,
            FocusMinutes = 30,
            FocusChime = false
        };
        var store = new FakeStore(seeded);
        var vm = new SettingsViewModel(store, new NullLog(), "Start with Windows")
        {
            StartWithWindows = true
        };

        await vm.SaveNowAsync();

        Assert.NotNull(store.Saved);
        Assert.Equal(seeded with { StartWithWindows = true }, store.Saved);
    }

    [Fact]
    public async Task FlushSavesPendingChange()
    {
        var store = new FakeStore();
        var vm = new SettingsViewModel(store, new NullLog(), "Start with test")
        {
            MinimalNotch = true
        };

        await vm.FlushAsync();

        Assert.NotNull(store.Saved);
        Assert.True(store.Saved!.MinimalNotch);
    }

    [Fact]
    public void NewerSchemaDisablesSave()
    {
        var store = new FakeStore { CanSave = false, Warning = "newer" };
        var vm = new SettingsViewModel(store, new NullLog(), "Start with Windows");

        Assert.False(vm.CanSave);
        Assert.Equal("newer", vm.Warning);
    }

    [Fact]
    public async Task AllFieldsRoundTrip()
    {
        var seeded = new AppSettings(
            SchemaVersion: SettingsSchema.CurrentVersion,
            StartWithWindows: true,
            MinimalNotch: true,
            HideInFullscreen: false,
            GlassBackground: false,
            BouncySpring: false,
            ShowMascot: false,
            VolumeToast: false,
            BrightnessToast: false,
            LyricsEnabled: false,
            LyricsHighlightColor: "#112233",
            WaterReminderEnabled: true,
            WaterIntervalMinutes: 45,
            WaterActiveStartHour: 7,
            WaterActiveEndHour: 21,
            WaterDailyGoal: 10,
            WaterGlassesToday: 3,
            WaterDate: "2026-11-17",
            CameraMirror: false,
            FocusMinutes: 40,
            ShortBreakMinutes: 7,
            LongBreakMinutes: 20,
            FocusChime: false);
        var store = new FakeStore(seeded);
        var vm = new SettingsViewModel(store, new NullLog(), "Start with Windows");

        await vm.SaveNowAsync();

        Assert.Equal(seeded, store.Saved);
    }

    [Fact]
    public async Task SaveFailureDoesNotRaiseSavedAndLogsWarning()
    {
        var store = new FakeStore { PersistSaves = false };
        var log = new RecordingLog();
        var vm = new SettingsViewModel(store, log, "Start with Windows");
        var saved = false;
        vm.Saved += (_, _) => saved = true;

        vm.MinimalNotch = true;
        await vm.SaveNowAsync();

        Assert.False(saved);
        Assert.False(vm.MinimalNotch);
        Assert.Contains(log.Warnings, message => message.Contains("did not persist", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PendingLocalEditSurvivesExternalRefresh()
    {
        var store = new FakeStore();
        var vm = new SettingsViewModel(store, new NullLog(), "Start with Windows");
        await vm.SaveNowAsync();

        vm.MinimalNotch = true;
        var external = SettingsSchema.Default with { GlassBackground = false };

        vm.RefreshFromStore(external);

        Assert.True(vm.MinimalNotch);
        Assert.False(vm.GlassBackground);

        await vm.SaveNowAsync();

        Assert.NotNull(store.Saved);
        Assert.True(store.Saved!.MinimalNotch);
        Assert.False(store.Saved.GlassBackground);
    }

    [Fact]
    public async Task LocalEditDuringSaveSurvivesNextSave()
    {
        var store = new FakeStore();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.SaveGate = gate.Task;
        var vm = new SettingsViewModel(store, new NullLog(), "Start with Windows");

        var saving = vm.SaveNowAsync();
        while (store.Saved is null)
        {
            await Task.Yield();
        }

        vm.GlassBackground = false;

        gate.SetResult();
        await saving;

        Assert.False(vm.GlassBackground);

        await vm.SaveNowAsync();

        Assert.NotNull(store.Saved);
        Assert.False(store.Saved!.GlassBackground);
    }

    private sealed class FakeStore : ISettingsStore
    {
        public FakeStore(AppSettings? current = null) => Current = current ?? SettingsSchema.Default;

        public AppSettings Current { get; private set; }
        public bool CanSave { get; set; } = true;
        public string? Warning { get; set; }
        string? ISettingsStore.LoadWarning => Warning;
        public event EventHandler<AppSettings>? Changed;

        public AppSettings? Saved { get; private set; }
        public bool PersistSaves { get; set; } = true;
        public Task? SaveGate { get; set; }

        public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;

        public async Task SaveAsync(AppSettings settings, CancellationToken ct = default)
        {
            Saved = settings;
            if (SaveGate is not null)
            {
                await SaveGate;
            }

            if (!PersistSaves)
            {
                return;
            }

            Current = settings;
            Changed?.Invoke(this, settings);
        }
    }

    private sealed class NullLog : IAppLog
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }

    private sealed class RecordingLog : IAppLog
    {
        public List<string> Warnings { get; } = new();

        public void Info(string message) { }
        public void Warn(string message) => Warnings.Add(message);
        public void Error(string message, Exception? exception = null) { }
    }
}
