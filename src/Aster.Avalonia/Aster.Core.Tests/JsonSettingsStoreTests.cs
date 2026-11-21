using Aster.Core.Interfaces;
using Aster.Core.Models;
using Aster.Core.Settings;

namespace Aster.Core.Tests;

public class JsonSettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "aster-settings-" + Guid.NewGuid().ToString("N"));
    private readonly IAppLog _log = new NullLog();

    public JsonSettingsStoreTests() => Directory.CreateDirectory(_directory);

    private string PathToSettings => Path.Combine(_directory, "settings.json");

    [Fact]
    public async Task MissingFileLoadsDefaults()
    {
        var store = new JsonSettingsStore(PathToSettings, _log);

        await store.LoadAsync();

        Assert.Equal(SettingsSchema.Default, store.Current);
        Assert.True(store.CanSave);
    }

    [Fact]
    public async Task SaveThenLoadRoundTrips()
    {
        var store = new JsonSettingsStore(PathToSettings, _log);
        await store.LoadAsync();
        var changed = SettingsTestData.NonDefault;

        await store.SaveAsync(changed);

        var reloaded = new JsonSettingsStore(PathToSettings, _log);
        await reloaded.LoadAsync();
        Assert.Equal(changed, reloaded.Current);
    }

    [Fact]
    public async Task SaveAlwaysWritesCurrentSchemaVersion()
    {
        var store = new JsonSettingsStore(PathToSettings, _log);
        await store.LoadAsync();

        await store.SaveAsync(SettingsTestData.NonDefault with { SchemaVersion = 1 });

        Assert.Equal(SettingsSchema.CurrentVersion, store.Current.SchemaVersion);
        var reloaded = new JsonSettingsStore(PathToSettings, _log);
        await reloaded.LoadAsync();
        Assert.Equal(SettingsTestData.NonDefault, reloaded.Current);
    }

    [Fact]
    public async Task MissingFieldsUseDefaults()
    {
        var json = """{"schemaVersion":1,"minimalNotch":true}""";
        await File.WriteAllTextAsync(PathToSettings, json);
        var store = new JsonSettingsStore(PathToSettings, _log);

        await store.LoadAsync();

        Assert.True(store.Current.MinimalNotch);
        Assert.True(store.Current.HideInFullscreen);
        Assert.True(store.Current.GlassBackground);
        Assert.True(store.Current.BouncySpring);
        Assert.False(store.Current.StartWithWindows);
    }

    [Fact]
    public async Task CorruptFileFallsBackToDefaultsAndIsBackedUp()
    {
        await File.WriteAllTextAsync(PathToSettings, "{ not json");
        var store = new JsonSettingsStore(PathToSettings, _log);

        await store.LoadAsync();

        Assert.Equal(SettingsSchema.Default, store.Current);
        Assert.True(store.CanSave);
        Assert.Contains(Directory.GetFiles(_directory), path => path.Contains(".corrupt-"));
    }

    [Fact]
    public async Task NewerSchemaDisablesSaveAndPreservesFile()
    {
        var newer = """{"schemaVersion":4,"startWithWindows":true}""";
        await File.WriteAllTextAsync(PathToSettings, newer);
        var store = new JsonSettingsStore(PathToSettings, _log);

        await store.LoadAsync();

        Assert.False(store.CanSave);
        Assert.NotNull(store.LoadWarning);
        Assert.Equal(newer, await File.ReadAllTextAsync(PathToSettings));
    }

    [Fact]
    public async Task UpdateAsyncAppliesInsideTheGate()
    {
        var store = new JsonSettingsStore(PathToSettings, _log);
        await store.LoadAsync();

        var result = await store.UpdateAsync(s => s with
        {
            FocusMinutes = 40,
            WaterGlassesToday = s.WaterGlassesToday + 1
        });

        Assert.True(result.Succeeded);
        Assert.Equal(40, store.Current.FocusMinutes);
        Assert.Equal(1, store.Current.WaterGlassesToday);
        Assert.Equal(SettingsSchema.CurrentVersion, store.Current.SchemaVersion);
    }

    [Fact]
    public async Task UpdateAsyncKeepsCurrentWhenReadOnly()
    {
        await File.WriteAllTextAsync(
            PathToSettings,
            """{"schemaVersion":99}""");
        var store = new JsonSettingsStore(PathToSettings, _log);
        await store.LoadAsync();

        var before = store.Current;
        var result = await store.UpdateAsync(s => s with { FocusMinutes = 40 });

        Assert.False(result.Succeeded);
        Assert.Same(before, result.Settings);
        Assert.Same(before, store.Current);
    }

    [Fact]
    public async Task ConcurrentUpdatesDoNotLoseIncrements()
    {
        var store = new JsonSettingsStore(PathToSettings, _log);
        await store.LoadAsync();

        await Task.WhenAll(
            store.UpdateAsync(s => s with { WaterGlassesToday = s.WaterGlassesToday + 1 }),
            store.UpdateAsync(s => s with { WaterGlassesToday = s.WaterGlassesToday + 1 }));

        Assert.Equal(2, store.Current.WaterGlassesToday);
    }

    private sealed class NullLog : IAppLog
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
