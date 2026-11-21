using Aster.Core.Interfaces;
using Aster.Core.Lyrics;
using Aster.Core.Models;

namespace Aster.Core.Tests;

public class LyricsServiceTests
{
    [Fact]
    public async Task CacheHitDoesNotCallProvider()
    {
        using var env = new Env();
        var document = new LyricsDocument
        {
            IsSynced = true,
            Lines = new List<LyricsLine> { new() { TimeSeconds = 1, Text = "Cached" } }
        };
        var snapshot = Snapshot();
        await env.Cache.WriteAsync(LyricsCache.KeyFor(snapshot), document, default);

        await env.Service.LoadAsync(snapshot);

        Assert.Equal(0, env.Provider.Calls);
        Assert.Equal("Cached", env.Service.Current!.Lines[0].Text);
    }

    [Fact]
    public async Task DisabledDoesNotCallProvider()
    {
        using var env = new Env(lyricsEnabled: false);

        await env.Service.LoadAsync(Snapshot());

        Assert.Equal(0, env.Provider.Calls);
        Assert.Null(env.Service.Current);
        Assert.Equal("Lyrics disabled in Settings", env.Service.StatusMessage);
    }

    [Fact]
    public async Task ProviderFailureSetsOffline()
    {
        using var env = new Env { Provider = { Throw = true } };

        await env.Service.LoadAsync(Snapshot());

        Assert.True(env.Service.HasError);
        Assert.Equal("Couldn't reach LRCLIB", env.Service.StatusMessage);
    }

    [Fact]
    public async Task SameTrackIsNotReloaded()
    {
        using var env = new Env();
        var snapshot = Snapshot();

        await env.Service.LoadAsync(snapshot);
        await env.Service.LoadAsync(snapshot);

        Assert.Equal(1, env.Provider.Calls);
    }

    [Fact]
    public async Task DisableClearsAndEnableReloads()
    {
        using var env = new Env();
        await env.Service.LoadAsync(Snapshot());
        Assert.NotNull(env.Service.Current);

        env.Settings.Current = env.Settings.Current with { LyricsEnabled = false };
        env.Settings.RaiseChanged();

        Assert.Null(env.Service.Current);
        Assert.Equal("Lyrics disabled in Settings", env.Service.StatusMessage);

        env.Settings.Current = env.Settings.Current with { LyricsEnabled = true };
        env.Settings.RaiseChanged();
        await Task.Delay(50);

        Assert.NotNull(env.Service.Current);
    }

    [Fact]
    public async Task ForceRefreshBypassesCache()
    {
        using var env = new Env();
        var snapshot = Snapshot();
        await env.Service.LoadAsync(snapshot);
        Assert.Equal(1, env.Provider.Calls);

        await env.Service.LoadAsync(snapshot, force: true);

        Assert.Equal(2, env.Provider.Calls);
    }

    private static MediaSnapshot Snapshot() => new(
        "Song", "Artist", "Album", null, "k", PlaybackState.Playing,
        TimeSpan.Zero, TimeSpan.FromMinutes(3), "Spotify",
        "track-1", true, true, true, DateTimeOffset.UtcNow);

    private sealed class Env : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "aster-lyrics-svc-" + Guid.NewGuid());
        public FakeProvider Provider { get; } = new();
        public FakeSettings Settings { get; } = new();
        public LyricsCache Cache { get; }
        public LyricsService Service { get; }

        public Env(bool lyricsEnabled = true)
        {
            Settings.Current = SettingsSchema.Default with { LyricsEnabled = lyricsEnabled };
            Cache = new LyricsCache(_dir, TimeProvider.System);
            Service = new LyricsService(Settings, Provider, Cache, new InlineDispatcher(), new NullLog());
        }

        public void Dispose()
        {
            Service.Dispose();
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, true);
            }
        }
    }

    private sealed class FakeProvider : ILyricsProvider
    {
        public int Calls { get; private set; }
        public bool Throw { get; set; }

        public Task<LyricsDocument?> GetAsync(LyricsQuery query, CancellationToken ct)
        {
            Calls++;
            if (Throw)
            {
                throw new HttpRequestException("offline");
            }

            return Task.FromResult<LyricsDocument?>(new LyricsDocument
            {
                IsSynced = true,
                Lines = new List<LyricsLine> { new() { TimeSeconds = 1, Text = "Fetched" } }
            });
        }
    }

    private sealed class FakeSettings : ISettingsStore
    {
        public AppSettings Current { get; set; } = SettingsSchema.Default;
        public bool CanSave => true;
        public string? LoadWarning => null;
        public event EventHandler<AppSettings>? Changed;
        public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveAsync(AppSettings settings, CancellationToken ct = default) => Task.CompletedTask;
        public Task<SettingsUpdateResult> UpdateAsync(
            Func<AppSettings, AppSettings> update,
            CancellationToken ct = default)
        {
            Current = update(Current) with
            {
                SchemaVersion = SettingsSchema.CurrentVersion
            };
            Changed?.Invoke(this, Current);
            return Task.FromResult(new SettingsUpdateResult(true, Current));
        }
        public void RaiseChanged() => Changed?.Invoke(this, Current);
    }

    private sealed class InlineDispatcher : IUiDispatcher
    {
        public bool CheckAccess() => true;
        public void Post(Action action) => action();
    }

    private sealed class NullLog : IAppLog
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }
}
