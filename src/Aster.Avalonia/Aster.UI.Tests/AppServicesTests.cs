using Aster.Core.Interfaces;
using Aster.Core.Models;
using Aster.UI.Abstractions;
using Avalonia.Controls;

namespace Aster.UI.Tests;

public class AppServicesTests
{
    [Fact]
    public async Task CreateAsyncReconcilesAutostartAndSavesOnce()
    {
        var store = new FakeSettingsStore(SettingsSchema.Default with { StartWithWindows = false });
        var factory = new FakePlatformFactory(store);
        factory.Autostart.IsEnabled = true;

        var services = await AppServices.CreateAsync(factory, new ImmediateDispatcher());

        Assert.Equal(1, store.SaveCount);
        var saved = Assert.Single(store.Saves);
        Assert.True(saved.StartWithWindows);
        Assert.True(store.Current.StartWithWindows);
        Assert.True(services.SettingsViewModel.StartWithWindows);
    }

    [Fact]
    public async Task CreateAsyncDoesNotSaveWhenAutostartAlreadyMatches()
    {
        var factory = new FakePlatformFactory();

        await AppServices.CreateAsync(factory, new ImmediateDispatcher());

        Assert.Equal(1, factory.Store.LoadCount);
        Assert.Equal(0, factory.Store.SaveCount);
    }

    [Fact]
    public async Task ShutdownFlushesSettingsAndDisposesServicesOnce()
    {
        var factory = new FakePlatformFactory();
        var services = await AppServices.CreateAsync(factory, new ImmediateDispatcher());
        services.SettingsViewModel.MinimalNotch = true;

        await services.ShutdownAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(1, factory.Store.SaveCount);
        Assert.True(Assert.Single(factory.Store.Saves).MinimalNotch);
        AssertAllDisposedOnce(factory);
    }

    [Fact]
    public async Task ShutdownFlushesSettingsBeforeDisposingServices()
    {
        var factory = new FakePlatformFactory();
        var services = await AppServices.CreateAsync(factory, new ImmediateDispatcher());
        services.SettingsViewModel.MinimalNotch = true;

        await services.ShutdownAsync(TimeSpan.FromSeconds(1));

        Assert.Equal("save", factory.Order[0]);
        Assert.True(factory.Order.IndexOf("effect") > 0);
        Assert.True(factory.Order.IndexOf("media") > 0);
    }

    [Fact]
    public async Task ShutdownIsIdempotent()
    {
        var factory = new FakePlatformFactory();
        var services = await AppServices.CreateAsync(factory, new ImmediateDispatcher());

        await services.ShutdownAsync(TimeSpan.FromSeconds(1));
        await services.ShutdownAsync(TimeSpan.FromSeconds(1));
        await services.DisposeAsync();

        Assert.Equal(1, factory.Store.SaveCount);
        AssertAllDisposedOnce(factory);
    }

    [Fact]
    public void ReconcileAutostartReturnsSameInstanceWhenUnchanged()
    {
        var settings = SettingsSchema.Default;

        var same = AppServices.ReconcileAutostart(settings, settings.StartWithWindows);
        var changed = AppServices.ReconcileAutostart(settings, !settings.StartWithWindows);

        Assert.Same(settings, same);
        Assert.NotSame(settings, changed);
        Assert.Equal(!settings.StartWithWindows, changed.StartWithWindows);
        Assert.Equal(settings.MinimalNotch, changed.MinimalNotch);
    }

    [Fact]
    public async Task ShutdownContinuesWhenFlushAndDisposalFail()
    {
        var factory = new FakePlatformFactory();
        var services = await AppServices.CreateAsync(factory, new ImmediateDispatcher());
        factory.Store.ThrowOnSave = true;
        factory.EffectLayer.ThrowOnDispose = true;

        await services.ShutdownAsync(TimeSpan.FromSeconds(1));

        AssertAllDisposedOnce(factory);
        Assert.Contains(
            factory.Log.Errors,
            message => message.Contains("flush", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            factory.Log.Errors,
            message => message.Contains("EffectLayer", StringComparison.Ordinal));
    }

    private static void AssertAllDisposedOnce(FakePlatformFactory factory)
    {
        Assert.Equal(1, factory.EffectLayer.DisposeCount);
        Assert.Equal(1, factory.Fullscreen.DisposeCount);
        Assert.Equal(1, factory.Media.DisposeCount);
        Assert.Equal(1, factory.Battery.DisposeCount);
        Assert.Equal(1, factory.Network.DisposeCount);
    }

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public bool CheckAccess() => true;

        public void Post(Action action) => action();
    }

    private sealed class RecordingLog : IAppLog
    {
        public List<string> Warnings { get; } = new();
        public List<string> Errors { get; } = new();

        public void Info(string message)
        {
        }

        public void Warn(string message) => Warnings.Add(message);

        public void Error(string message, Exception? exception = null) => Errors.Add(message);
    }

    private sealed class FakeSettingsStore : ISettingsStore
    {
        public FakeSettingsStore(AppSettings? current = null) => Current = current ?? SettingsSchema.Default;

        public AppSettings Current { get; private set; }
        public bool CanSave => true;
        public string? LoadWarning => null;
        public int LoadCount { get; private set; }
        public int SaveCount { get; private set; }
        public List<AppSettings> Saves { get; } = new();
        public bool ThrowOnSave { get; set; }
        public List<string>? Order { get; set; }

        public event EventHandler<AppSettings>? Changed;

        public Task LoadAsync(CancellationToken ct = default)
        {
            LoadCount++;
            return Task.CompletedTask;
        }

        public Task SaveAsync(AppSettings settings, CancellationToken ct = default)
        {
            SaveCount++;
            Order?.Add("save");
            if (ThrowOnSave)
            {
                throw new InvalidOperationException("Settings save failed.");
            }

            Saves.Add(settings);
            Current = settings;
            Changed?.Invoke(this, settings);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAutostart : IAutostart
    {
        public bool IsEnabled { get; set; }

        public bool SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
            return true;
        }
    }

    private sealed class FakeMediaSession : IMediaSession, IDisposable
    {
        public MediaSnapshot Current { get; set; } = MediaSnapshot.Empty;
        public int StartCount { get; private set; }
        public int DisposeCount { get; private set; }
        public List<string>? Order { get; set; }

        public event EventHandler<MediaSnapshot>? Changed
        {
            add { }
            remove { }
        }

        public event EventHandler<MediaSnapshot>? PropertiesChanged
        {
            add { }
            remove { }
        }

        public Task PlayPauseAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task NextAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task PreviousAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task SeekAsync(TimeSpan position, CancellationToken ct = default) => Task.CompletedTask;

        public TimeSpan EstimatedPosition(DateTimeOffset now) => TimeSpan.Zero;

        public void Start() => StartCount++;

        public void Dispose()
        {
            Order?.Add("media");
            DisposeCount++;
        }
    }

    private sealed class FakeBatteryStatus : IBatteryStatus, IDisposable
    {
        public BatterySnapshot Current { get; } = new(80, false, true);
        public int DisposeCount { get; private set; }

        public event EventHandler<BatterySnapshot>? Changed
        {
            add { }
            remove { }
        }

        public void Dispose() => DisposeCount++;
    }

    private sealed class FakeNetworkStatus : INetworkStatus, IDisposable
    {
        public bool IsConnected => true;
        public int DisposeCount { get; private set; }

        public event EventHandler<bool>? Changed
        {
            add { }
            remove { }
        }

        public void Dispose() => DisposeCount++;
    }

    private sealed class FakeFullscreenWatcher : IFullscreenWatcher, IDisposable
    {
        public bool IsFullscreen => false;
        public int DisposeCount { get; private set; }

        public event EventHandler<bool>? Changed
        {
            add { }
            remove { }
        }

        public void Dispose() => DisposeCount++;
    }

    private sealed class FakeEffectLayer : IEffectLayer
    {
        public bool IsAvailable => true;
        public int DisposeCount { get; private set; }
        public bool ThrowOnDispose { get; set; }
        public List<string>? Order { get; set; }

        public void Attach(Window island)
        {
        }

        public void Update(EffectLayerState state)
        {
        }

        public void Dispose()
        {
            Order?.Add("effect");
            DisposeCount++;
            if (ThrowOnDispose)
            {
                throw new InvalidOperationException("Effect layer dispose failed.");
            }
        }
    }

    private sealed class FakePlacement : IWindowPlacement
    {
        public bool IsCursorInside(IntPtr handle) => false;

        public uint GetDpi(IntPtr handle) => 96;

        public void SetBounds(IntPtr handle, int x, int y, int width, int height)
        {
        }
    }

    private sealed class FakeChrome : IWindowChrome
    {
        public void Attach(Window window)
        {
        }
    }

    private sealed class FakeSingleInstance : ISingleInstance
    {
        public bool IsFirstInstance => true;

        public void Dispose()
        {
        }
    }

    private sealed class FakePlatformFactory : IPlatformFactory
    {
        public FakePlatformFactory(FakeSettingsStore? store = null)
        {
            Store = store ?? new FakeSettingsStore();
            Store.Order = Order;
            Media.Order = Order;
            EffectLayer.Order = Order;
        }

        public List<string> Order { get; } = new();

        public string PlatformName => "Test";
        public string AutostartLabel => "Start with Aster";
        public CapabilityMatrix Capabilities { get; } =
            new(Capability.Media | Capability.Battery | Capability.Network | Capability.Fullscreen);

        public RecordingLog Log { get; } = new();
        public FakeSettingsStore Store { get; }
        public FakeAutostart Autostart { get; } = new();
        public FakeMediaSession Media { get; } = new();
        public FakeBatteryStatus Battery { get; } = new();
        public FakeNetworkStatus Network { get; } = new();
        public FakeFullscreenWatcher Fullscreen { get; } = new();
        public FakeEffectLayer EffectLayer { get; } = new();
        public FakePlacement Placement { get; } = new();
        public FakeChrome Chrome { get; } = new();
        public FakeSingleInstance SingleInstance { get; } = new();

        public IAppLog CreateLog() => Log;

        public ISettingsStore CreateSettingsStore(IAppLog log) => Store;

        public IMediaSession CreateMediaSession(IUiDispatcher dispatcher, IAppLog log) => Media;

        public IBatteryStatus CreateBatteryStatus(IUiDispatcher dispatcher, IAppLog log) => Battery;

        public INetworkStatus CreateNetworkStatus(IUiDispatcher dispatcher, IAppLog log) => Network;

        public IAutostart CreateAutostart(IAppLog log) => Autostart;

        public IFullscreenWatcher CreateFullscreenWatcher(IUiDispatcher dispatcher, IAppLog log) => Fullscreen;

        public IWindowPlacement CreateWindowPlacement(IAppLog log) => Placement;

        public IWindowChrome CreateWindowChrome(IAppLog log) => Chrome;

        public ISingleInstance CreateSingleInstance(string name) => SingleInstance;

        public IEffectLayer CreateEffectLayer(IAppLog log) => EffectLayer;
    }
}
