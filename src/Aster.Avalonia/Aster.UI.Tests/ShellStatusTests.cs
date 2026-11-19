using Aster.UI.Controls;
using Aster.UI.ViewModels;
using Aster.Core.Interfaces;
using Aster.Core.Models;

namespace Aster.UI.Tests;

public class ShellStatusTests
{
    [Fact]
    public void BatteryAndNetworkUpdate()
    {
        var battery = new FakeBattery();
        var network = new FakeNetwork();
        var vm = new ShellViewModel(battery, network);

        battery.Update(new BatterySnapshot(82, true, true));
        network.Update(true);

        Assert.Equal("82%+", vm.BatteryText);
        Assert.True(vm.IsWifiConnected);
        Assert.Equal("Wi-Fi", vm.WifiText);
        Assert.True(vm.Capabilities.Has(Capability.Media));
    }

    [Fact]
    public void NoBatteryHidesText()
    {
        var battery = new FakeBattery();
        var vm = new ShellViewModel(battery, new FakeNetwork());

        battery.Update(new BatterySnapshot(100, false, false));

        Assert.Equal(string.Empty, vm.BatteryText);
    }

    [Fact]
    public void NoBatteryStillNotifiesBatteryProperties()
    {
        var battery = new FakeBattery();
        var vm = new ShellViewModel(battery, new FakeNetwork());
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        battery.Update(new BatterySnapshot(100, false, false));

        Assert.False(vm.HasBattery);
        Assert.Contains(nameof(ShellViewModel.BatteryPercent), changed);
        Assert.Contains(nameof(ShellViewModel.IsCharging), changed);
        Assert.Contains(nameof(ShellViewModel.HasBattery), changed);
    }

    [Fact]
    public void PlayGlyphTracksIsPlaying()
    {
        var media = new FakeMedia(Snapshot(PlaybackState.Playing));
        var mediaViewModel = new MediaViewModel(media, new InlineDispatcher(), new NullLog());
        var vm = new ShellViewModel(media: mediaViewModel);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        Assert.Equal(Glyph.Get(Glyph.Kind.Pause), vm.PlayGlyph);

        media.Update(Snapshot(PlaybackState.Paused));

        Assert.Equal(Glyph.Get(Glyph.Kind.Play), vm.PlayGlyph);
        Assert.Contains(nameof(ShellViewModel.PlayGlyph), changed);
    }

    [Fact]
    public void WifiGlyphAndOpacityFollowConnection()
    {
        var network = new FakeNetwork();
        var vm = new ShellViewModel(new FakeBattery(), network);

        network.Update(false);

        Assert.Equal(Glyph.Get(Glyph.Kind.Wifi), vm.WifiGlyph);
        Assert.Equal(0.25, vm.WifiOpacity);

        network.Update(true);

        Assert.Equal(1, vm.WifiOpacity);
    }

    private static MediaSnapshot Snapshot(PlaybackState playback) => new(
        "Track", "Artist", string.Empty, null, "key", playback,
        TimeSpan.Zero, TimeSpan.FromMinutes(3), "Spotify",
        "track-1", true, true, true, DateTimeOffset.Now);

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

        public void Update(MediaSnapshot snapshot)
        {
            Current = snapshot;
            Changed?.Invoke(this, snapshot);
        }
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

    private sealed class FakeBattery : IBatteryStatus
    {
        public BatterySnapshot Current { get; private set; } = new(100, false, true);
        public event EventHandler<BatterySnapshot>? Changed;

        public void Update(BatterySnapshot snapshot)
        {
            Current = snapshot;
            Changed?.Invoke(this, snapshot);
        }
    }

    private sealed class FakeNetwork : INetworkStatus
    {
        public bool IsConnected { get; private set; }
        public event EventHandler<bool>? Changed;

        public void Update(bool connected)
        {
            IsConnected = connected;
            Changed?.Invoke(this, connected);
        }
    }
}
