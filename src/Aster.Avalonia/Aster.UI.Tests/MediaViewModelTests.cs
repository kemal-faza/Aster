using Aster.UI.ViewModels;
using Aster.Core.Interfaces;
using Aster.Core.Logic;
using Aster.Core.Models;

#pragma warning disable CS0067 // FakeMedia raises Changed from Update; PropertiesChanged stays unused.

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

    [Fact]
    public void SpectrumStartsAndStopsWithPlayback()
    {
        var snapshot = new MediaSnapshot(
            "Track", "Artist", "", null, "key", PlaybackState.Playing,
            TimeSpan.Zero, TimeSpan.FromMinutes(3), "Spotify",
            "track-1", true, true, true, DateTimeOffset.Now);
        var media = new FakeMedia(snapshot);
        var spectrum = new FakeSpectrum();
        var vm = new MediaViewModel(media, new InlineDispatcher(), new NullLog(), spectrum);

        Assert.Equal(1, spectrum.StartCount);

        media.Update(snapshot with { Playback = PlaybackState.Paused });
        Assert.Equal(1, spectrum.StopCount);
    }

    [Fact]
    public async Task TurningAudioReactiveOffStopsCaptureAndUsesTheSimulation()
    {
        var snapshot = new MediaSnapshot(
            "Track", "Artist", "", null, "key", PlaybackState.Playing,
            TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(3), "Spotify",
            "track-1", true, true, true, DateTimeOffset.Now);
        var spectrum = new FakeSpectrum();
        var settings = new FakeSettings(SettingsSchema.Default with { AudioReactiveBars = true });
        var vm = new MediaViewModel(
            new FakeMedia(snapshot), new InlineDispatcher(), new NullLog(), spectrum, settings);

        Assert.Equal(1, spectrum.StartCount);

        await settings.SaveAsync(settings.Current with { AudioReactiveBars = false });

        Assert.Equal(1, spectrum.StopCount);
        Assert.InRange(vm.SpectrumEnergy, 0, 1);
        Assert.NotEqual(SpectrumFrame.Silence, vm.Spectrum);

        await settings.SaveAsync(settings.Current with { AudioReactiveBars = true });

        Assert.Equal(2, spectrum.StartCount);
    }

    [Fact]
    public async Task DisposeStopsTheSourceAndUnsubscribesFromSettings()
    {
        var snapshot = new MediaSnapshot(
            "Track", "Artist", "", null, "key", PlaybackState.Playing,
            TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(3), "Spotify",
            "track-1", true, true, true, DateTimeOffset.Now);
        var spectrum = new FakeSpectrum();
        var settings = new FakeSettings(SettingsSchema.Default with { AudioReactiveBars = true });
        var vm = new MediaViewModel(
            new FakeMedia(snapshot), new InlineDispatcher(), new NullLog(), spectrum, settings);

        vm.Dispose();
        Assert.Equal(1, spectrum.StopCount);

        await settings.SaveAsync(settings.Current with { AudioReactiveBars = false });

        Assert.Equal(1, spectrum.StopCount);
    }

    private sealed class FakeSpectrum : IAudioSpectrumSource
    {
        public bool IsAvailable { get; private set; }
        public SpectrumFrame Latest { get; private set; }
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }

        public void Start()
        {
            StartCount++;
            IsAvailable = true;
        }

        public void Stop()
        {
            StopCount++;
            IsAvailable = false;
            Latest = SpectrumFrame.Silence;
        }

        public void Dispose() => Stop();
    }

    private sealed class FakeSettings : ISettingsStore
    {
        public FakeSettings(AppSettings current) => Current = current;

        public AppSettings Current { get; private set; }
        public bool CanSave => true;
        public string? LoadWarning => null;
        public int SaveCount { get; private set; }

        public event EventHandler<AppSettings>? Changed;

        public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task SaveAsync(AppSettings settings, CancellationToken ct = default)
        {
            Current = settings;
            SaveCount++;
            Changed?.Invoke(this, settings);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeMedia : IMediaSession
    {
        public FakeMedia(MediaSnapshot snapshot) => Current = snapshot;
        public MediaSnapshot Current { get; private set; }

        public void Update(MediaSnapshot snapshot)
        {
            Current = snapshot;
            Changed?.Invoke(this, snapshot);
        }

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
