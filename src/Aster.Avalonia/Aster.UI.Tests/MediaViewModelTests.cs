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

    [Fact]
    public void PlaybackTickOnlyFormatsTheTextWhenTheSecondChanges()
    {
        var first = MediaViewModel.ComputePlaybackTick(
            TimeSpan.FromSeconds(12.2), TimeSpan.FromSeconds(180), lastSecond: -1);
        Assert.Equal(12.2, first.PositionSeconds, 3);
        Assert.Equal("0:12", first.PositionText);
        Assert.Equal(12, first.WholeSecond);

        var sameSecond = MediaViewModel.ComputePlaybackTick(
            TimeSpan.FromSeconds(12.8), TimeSpan.FromSeconds(180), lastSecond: 12);
        Assert.Null(sameSecond.PositionText);
        Assert.Equal(12.8, sameSecond.PositionSeconds, 3);

        var nextSecond = MediaViewModel.ComputePlaybackTick(
            TimeSpan.FromSeconds(13.0), TimeSpan.FromSeconds(180), lastSecond: 12);
        Assert.Equal("0:13", nextSecond.PositionText);
        Assert.Equal(13, nextSecond.WholeSecond);
    }

    [Fact]
    public void PlaybackTickClampsProgress()
    {
        var beyond = MediaViewModel.ComputePlaybackTick(
            TimeSpan.FromSeconds(200), TimeSpan.FromSeconds(180), lastSecond: -1);
        Assert.Equal(1, beyond.Progress);

        var noDuration = MediaViewModel.ComputePlaybackTick(
            TimeSpan.FromSeconds(10), TimeSpan.Zero, lastSecond: -1);
        Assert.Equal(0, noDuration.Progress);
    }

    [Fact]
    public async Task SeekUpdatesOptimisticallyAndClearsIsSeeking()
    {
        var snapshot = new MediaSnapshot(
            "Track", "Artist", "", null, "key", PlaybackState.Playing,
            TimeSpan.Zero, TimeSpan.FromMinutes(3), "Spotify",
            "track-1", true, true, true, DateTimeOffset.Now);
        var media = new FakeMedia(snapshot);
        var vm = new MediaViewModel(media, new InlineDispatcher(), new NullLog());

        await vm.SeekCommand.ExecuteAsync(30.0);

        Assert.Equal(30.0, vm.PositionSeconds, 3);
        Assert.Equal(30.0 / 180.0, vm.Progress, 3);
        Assert.False(vm.IsSeeking);
    }

    [Fact]
    public async Task SeekKeepsTheOptimisticPositionWhileThePlayerIsDelayed()
    {
        var snapshot = new MediaSnapshot(
            "Track", "Artist", "", null, "key", PlaybackState.Playing,
            TimeSpan.Zero, TimeSpan.FromMinutes(3), "Spotify",
            "track-1", true, true, true, DateTimeOffset.Now);
        var media = new FakeMedia(snapshot)
        {
            SeekGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var vm = new MediaViewModel(media, new InlineDispatcher(), new NullLog());

        var seek = vm.SeekCommand.ExecuteAsync(30.0);
        await Task.Delay(50);

        Assert.True(vm.IsSeeking);
        Assert.Equal(30.0, vm.PositionSeconds, 3);

        media.SeekGate.SetResult();
        await seek;

        Assert.False(vm.IsSeeking);
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

        public TaskCompletionSource? SeekGate { get; set; }

        public async Task SeekAsync(TimeSpan position, CancellationToken ct = default)
        {
            if (SeekGate is not null)
            {
                await SeekGate.Task.WaitAsync(ct);
            }

            Current = Current with
            {
                Position = position,
                UpdatedAt = DateTimeOffset.Now
            };
        }

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
