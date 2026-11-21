using Aster.Core.Interfaces;
using Aster.Core.Lyrics;
using Aster.Core.Models;
using Aster.UI.ViewModels;

namespace Aster.UI.Tests;

public class LyricsViewModelTests
{
    [Fact]
    public async Task ActiveIndexFollowsPosition()
    {
        using var env = new Env();
        await env.Service.LoadAsync(env.Snapshot);
        env.MediaSession.Update(env.Snapshot with { Position = TimeSpan.FromSeconds(2) });
        env.MediaSession.Update(env.Snapshot with { Position = TimeSpan.FromSeconds(11) });

        Assert.Equal(1, env.ViewModel.ActiveIndex);
        Assert.False(env.ViewModel.Lines[0].IsActive);
        Assert.True(env.ViewModel.Lines[1].IsActive);
    }

    private sealed class Env : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "aster-lyrics-vm-" + Guid.NewGuid());
        public MediaSnapshot Snapshot { get; } = new(
            "Song", "Artist", "Album", null, "k", PlaybackState.Playing,
            TimeSpan.Zero, TimeSpan.FromMinutes(3), "Spotify",
            "track", true, true, true, DateTimeOffset.UtcNow);
        public FakeMedia MediaSession { get; } = new();
        public MediaViewModel Media { get; }
        public LyricsService Service { get; }
        public LyricsViewModel ViewModel { get; }

        public Env()
        {
            MediaSession.Current = Snapshot;
            Media = new MediaViewModel(MediaSession, new InlineDispatcher(), new NullLog());
            var settingsStore = new FakeSettings();
            var settings = new SettingsViewModel(settingsStore, new NullLog(), "Start with system");
            var cache = new LyricsCache(_dir, TimeProvider.System);
            Service = new LyricsService(settingsStore, new FakeProvider(), cache,
                new InlineDispatcher(), new NullLog());
            ViewModel = new LyricsViewModel(Service, Media, settings);
        }

        public void Dispose()
        {
            ViewModel.Dispose();
            Service.Dispose();
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }
    }

    private sealed class FakeMedia : IMediaSession
    {
        public MediaSnapshot Current { get; set; } = MediaSnapshot.Empty;
        public event EventHandler<MediaSnapshot>? Changed;
        public event EventHandler<MediaSnapshot>? PropertiesChanged { add { } remove { } }
        public Task PlayPauseAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task NextAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task PreviousAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task SeekAsync(TimeSpan position, CancellationToken ct = default) => Task.CompletedTask;
        public TimeSpan EstimatedPosition(DateTimeOffset now) => Current.Position;
        public void Start() { }

        public void Update(MediaSnapshot snapshot)
        {
            Current = snapshot;
            Changed?.Invoke(this, snapshot);
        }
    }

    private sealed class FakeProvider : ILyricsProvider
    {
        public Task<LyricsDocument?> GetAsync(LyricsQuery query, CancellationToken ct) =>
            Task.FromResult<LyricsDocument?>(new LyricsDocument
            {
                IsSynced = true,
                Lines = new List<LyricsLine>
                {
                    new() { TimeSeconds = 0, Text = "One" },
                    new() { TimeSeconds = 10, Text = "Two" }
                }
            });
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
