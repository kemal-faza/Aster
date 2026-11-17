using Aster.Core.Interfaces;
using Aster.Core.Models;
using Aster.UI.Abstractions;
using Aster.UI.Services;
using Aster.UI.ViewModels;
using Aster.UI.Views;

namespace Aster.UI;

public sealed class AppServices : IAsyncDisposable
{
    private readonly SemaphoreSlim _shutdownGate = new(1, 1);
    private bool _shutdownComplete;

    public IAppLog Log { get; }
    public ISettingsStore Settings { get; }
    public IMediaSession Media { get; }
    public IBatteryStatus Battery { get; }
    public INetworkStatus Network { get; }
    public IAutostart Autostart { get; }
    public IFullscreenWatcher Fullscreen { get; }
    public IWindowPlacement Placement { get; }
    public IWindowChrome Chrome { get; }
    public IEffectLayer EffectLayer { get; }
    public ShellViewModel Shell { get; }
    public SettingsViewModel SettingsViewModel { get; }
    public WindowMotionController? Motion { get; private set; }
    public TrayService? Tray { get; private set; }
    public bool TrayFailed { get; private set; }

    private AppServices(
        IPlatformFactory factory,
        IUiDispatcher dispatcher,
        IAppLog log,
        ISettingsStore settings,
        IAutostart autostart)
    {
        Log = log;
        Settings = settings;
        Autostart = autostart;
        Media = factory.CreateMediaSession(dispatcher, log);
        Battery = factory.CreateBatteryStatus(dispatcher, log);
        Network = factory.CreateNetworkStatus(dispatcher, log);
        Fullscreen = factory.CreateFullscreenWatcher(dispatcher, log);
        Placement = factory.CreateWindowPlacement(log);
        Chrome = factory.CreateWindowChrome(log);
        EffectLayer = factory.CreateEffectLayer(log);

        var media = new MediaViewModel(Media, dispatcher, log);
        Shell = new ShellViewModel(Battery, Network, media, factory.Capabilities);
        SettingsViewModel = new SettingsViewModel(settings, log, factory.AutostartLabel);
    }

    public static async Task<AppServices> CreateAsync(
        IPlatformFactory factory,
        IUiDispatcher dispatcher,
        CancellationToken ct = default)
    {
        var log = factory.CreateLog();
        var settings = factory.CreateSettingsStore(log);
        await settings.LoadAsync(ct);

        var autostart = factory.CreateAutostart(log);
        var reconciled = ReconcileAutostart(settings.Current, autostart.IsEnabled);
        if (!ReferenceEquals(reconciled, settings.Current))
        {
            await settings.SaveAsync(reconciled, ct);
        }

        return new AppServices(factory, dispatcher, log, settings, autostart);
    }

    public static AppSettings ReconcileAutostart(AppSettings settings, bool autostartEnabled) =>
        settings.StartWithWindows == autostartEnabled
            ? settings
            : settings with { StartWithWindows = autostartEnabled };

    public void AttachWindow(MainWindow window)
    {
        Motion = new WindowMotionController(window, Shell, Placement, Settings, Log);

        var commands = new TrayCommandHandler(
            toggleVisibility: window.ToggleVisibility,
            isAutostart: () => Autostart.IsEnabled,
            setAutostart: enabled =>
            {
                if (!Autostart.SetEnabled(enabled))
                {
                    return false;
                }

                _ = Settings.SaveAsync(Settings.Current with { StartWithWindows = enabled });
                return true;
            },
            openSettings: window.ShowSettings,
            quit: window.Quit);

        try
        {
            Tray = new TrayService(commands, () => Autostart.IsEnabled);
        }
        catch (Exception ex)
        {
            TrayFailed = true;
            Log.Error("Tray creation failed.", ex);
        }
    }

    public void Start()
    {
        Media.Start();
        Motion?.Start();
    }

    public async ValueTask DisposeAsync() => await ShutdownAsync(TimeSpan.FromSeconds(5));

    public async Task ShutdownAsync(TimeSpan timeout)
    {
        await _shutdownGate.WaitAsync();
        try
        {
            if (_shutdownComplete)
            {
                return;
            }

            await FlushSettingsAsync(timeout);

            DisposeService(nameof(Motion), Motion, expectDisposable: true);
            DisposeService(nameof(Tray), Tray, expectDisposable: true);
            DisposeService(nameof(EffectLayer), EffectLayer, expectDisposable: true);
            DisposeService(nameof(Fullscreen), Fullscreen);
            DisposeService(nameof(Media), Media);
            DisposeService(nameof(Battery), Battery);
            DisposeService(nameof(Network), Network);

            _shutdownComplete = true;
        }
        finally
        {
            _shutdownGate.Release();
        }
    }

    private async Task FlushSettingsAsync(TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await SettingsViewModel.FlushAsync().WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Log.Warn("Settings flush timed out during shutdown.");
        }
        catch (Exception ex)
        {
            Log.Error("Settings flush failed during shutdown.", ex);
        }
    }

    private void DisposeService(string name, object? service, bool expectDisposable = false)
    {
        if (service is null)
        {
            return;
        }

        if (service is IDisposable disposable)
        {
            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to dispose {name}.", ex);
            }

            return;
        }

        if (expectDisposable)
        {
            Log.Warn($"{name} is not disposable; skipping disposal.");
        }
    }
}
