using Aster.Core.Interfaces;
using Aster.UI.Abstractions;
using Aster.UI.Services;
using Aster.UI.ViewModels;
using Aster.UI.Views;

namespace Aster.UI;

public sealed class AppServices : IAsyncDisposable
{
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
        if (autostart.IsEnabled != settings.Current.StartWithWindows)
        {
            var reconciled = settings.Current with { StartWithWindows = autostart.IsEnabled };
            await settings.SaveAsync(reconciled, ct);
        }

        return new AppServices(factory, dispatcher, log, settings, autostart);
    }

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
        Motion?.Dispose();
        Tray?.Dispose();
        EffectLayer.Dispose();
        (Fullscreen as IDisposable)?.Dispose();
        (Media as IDisposable)?.Dispose();
        (Battery as IDisposable)?.Dispose();
        (Network as IDisposable)?.Dispose();

        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await SettingsViewModel.FlushAsync().WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Log.Warn("Settings flush timed out during shutdown.");
        }
    }
}
