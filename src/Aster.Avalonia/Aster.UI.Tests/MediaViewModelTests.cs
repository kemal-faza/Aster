using Aster.UI.ViewModels;
using Aster.Core.Interfaces;
using Aster.Core.Logic;
using Aster.Core.Models;

#pragma warning disable CS0067 // FakeMedia never raises Changed; the interface requires the event.

namespace Aster.UI.Tests;

public class MediaViewModelTests
{
    [Fact]
    public void SnapshotExposesIdentityCapabilitiesAndSeed()
    {
        var snapshot = new MediaSnapshot(
            "Track", "Artist", "", null, "key", PlaybackState.Playing,
            TimeSpan.Zero, TimeSpan.FromMinutes(3), "Spotify",
            "track-1", false, true, false, DateTimeOffset.Now);
        var vm = new MediaViewModel(new FakeMedia(snapshot), new InlineDispatcher(), new NullLog());

        Assert.Equal("track-1", vm.TrackKey);
        Assert.Equal(TrackHash.Of("Track|Artist|"), vm.WaveformSeed);
        Assert.False(vm.CanSeek);
        Assert.True(vm.CanGoNext);
        Assert.False(vm.CanGoPrevious);
        Assert.Equal("Track", vm.DisplayTitle);
        Assert.Equal("Artist", vm.DisplayArtist);
    }

    [Fact]
    public void EmptySnapshotUsesEmptyStateCopy()
    {
        var vm = new MediaViewModel(new FakeMedia(MediaSnapshot.Empty),
            new InlineDispatcher(), new NullLog());

        Assert.Equal("Nothing playing", vm.DisplayTitle);
        Assert.Equal("Start something on Spotify, YouTube, or Apple Music", vm.DisplayArtist);
        Assert.Equal(0, vm.WaveformSeed);
    }

    [Fact]
    public void EmptySnapshotShowsNoSession()
    {
        var media = new FakeMedia(MediaSnapshot.Empty);
        var vm = new MediaViewModel(media, new InlineDispatcher(), new NullLog());

        Assert.False(vm.HasSession);
    }

    [Fact]
    public void StoppedSnapshotShowsNoSession()
    {
        var snapshot = new MediaSnapshot(
            "Track", "Artist", "", null, "key", PlaybackState.Stopped,
            TimeSpan.Zero, TimeSpan.FromMinutes(3), "Spotify",
            "track-1", true, true, true, DateTimeOffset.Now);
        var vm = new MediaViewModel(new FakeMedia(snapshot), new InlineDispatcher(), new NullLog());

        Assert.False(vm.HasSession);
    }

    [Fact]
    public void AlbumBecomesArtistFallback()
    {
        var snapshot = new MediaSnapshot(
            "Track", "", "Album", null, "key", PlaybackState.Paused,
            TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(3), "Spotify",
            "track-1", true, true, true, DateTimeOffset.Now);
        var vm = new MediaViewModel(new FakeMedia(snapshot), new InlineDispatcher(), new NullLog());

        Assert.True(vm.HasSession);
        Assert.Equal("Track", vm.Title);
        Assert.Equal("Album", vm.Artist);
        Assert.Equal("Spotify", vm.SourceApp);
    }

    private sealed class FakeMedia : IMediaSession
    {
        public FakeMedia(MediaSnapshot snapshot) => Current = snapshot;
        public MediaSnapshot Current { get; private set; }
        public event EventHandler<MediaSnapshot>? Changed;
        public event EventHandler<MediaSnapshot>? PropertiesChanged
        {
            add { }
            remove { }
        }
        public Task PlayPauseAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task NextAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task PreviousAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task SeekAsync(TimeSpan position, CancellationToken ct = default) => Task.CompletedTask;
        public TimeSpan EstimatedPosition(DateTimeOffset now) => Current.Position;
        public void Start() { }
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
