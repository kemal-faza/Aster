using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Aster.Core;
using Aster.Core.Logging;
using Aster.UI.Abstractions;
using Aster.UI.Services;
using Aster.UI.Views;

namespace Aster.UI;

public partial class App : Application
{
    public static IPlatformFactory PlatformFactory { get; set; } = null!;
    public static Stopwatch StartupClock { get; } = new();

    private AppServices? _services;
    private bool _shuttingDown;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LogCrash("Unhandled exception.", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogCrash("Unobserved task exception.", e.Exception);
            e.SetObserved();
        };
        Dispatcher.UIThread.UnhandledException += (_, e) =>
            LogCrash("Dispatcher exception.", e.Exception);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _ = InitializeAsync(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task InitializeAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            if (PlatformFactory is null)
            {
                LogCrash("Startup failed: no platform factory is configured.", null);
                desktop.Shutdown(1);
                return;
            }

            var dispatcher = new AvaloniaUiDispatcher();
            _services = await AppServices.CreateAsync(PlatformFactory, dispatcher);
            var window = new MainWindow(_services);
            desktop.MainWindow = window;
            desktop.ShutdownRequested += OnShutdownRequested;
            window.Show();
        }
        catch (Exception ex)
        {
            LogCrash("Startup failed.", ex);
            if (_services is { } services)
            {
                try
                {
                    await services.ShutdownAsync(TimeSpan.FromSeconds(5));
                }
                catch (Exception shutdownEx)
                {
                    LogCrash("Startup cleanup failed.", shutdownEx);
                }
                finally
                {
                    _services = null;
                }
            }

            desktop.Shutdown(1);
        }
    }

    private async void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        if (_services is null)
        {
            return;
        }

        e.Cancel = true;
        if (_shuttingDown)
        {
            return;
        }

        _shuttingDown = true;
        try
        {
            await _services.ShutdownAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
        }
    }

    private static void LogCrash(string message, Exception? exception)
    {
        try
        {
            new FileAppLog(AppPaths.LogPath).Error(message, exception);
        }
        catch
        {
            // Crash logging must never crash the process.
        }
    }
}
