using Aster.Core.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace Aster.UI.Services;

public sealed class TrayService : IDisposable
{
    private readonly TrayIcon _tray;
    private readonly NativeMenuItem _start;

    public TrayService(
        TrayCommandHandler commands,
        Func<bool> isAutostart,
        Func<AppSettings> settings,
        Func<bool> islandVisible,
        string autostartLabel)
    {
        var menu = new NativeMenu();

        var toggle = new NativeMenuItem("Show/Hide");
        toggle.Click += (_, _) =>
        {
            commands.ToggleVisibility();
            toggle.Header = islandVisible() ? "Hide Aster" : "Show Aster";
        };

        _start = new NativeMenuItem(autostartLabel)
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = isAutostart()
        };
        _start.Click += (_, _) =>
        {
            _start.IsChecked = commands.ToggleAutostart();
        };

        var minimal = new NativeMenuItem("Minimal notch")
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = settings().MinimalNotch
        };
        minimal.Click += (_, _) =>
        {
            var enabled = !settings().MinimalNotch;
            commands.SetMinimal(enabled);
            minimal.IsChecked = enabled;
        };

        var mascot = new NativeMenuItem("Show mascot")
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = settings().ShowMascot
        };
        mascot.Click += (_, _) =>
        {
            var enabled = !settings().ShowMascot;
            commands.SetMascot(enabled);
            mascot.IsChecked = enabled;
        };

        var fullscreen = new NativeMenuItem("Hide in fullscreen")
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = settings().HideInFullscreen
        };
        fullscreen.Click += (_, _) =>
        {
            var enabled = !settings().HideInFullscreen;
            commands.SetFullscreen(enabled);
            fullscreen.IsChecked = enabled;
        };

        var settingsItem = new NativeMenuItem("Settings...");
        settingsItem.Click += (_, _) => commands.OpenSettings();

        var about = new NativeMenuItem("About Aster");
        about.Click += (_, _) => commands.ShowAbout();

        var quit = new NativeMenuItem("Exit");
        quit.Click += (_, _) => commands.Quit();

        menu.Items.Add(toggle);
        menu.Items.Add(_start);
        menu.Items.Add(minimal);
        menu.Items.Add(mascot);
        menu.Items.Add(fullscreen);
        menu.Items.Add(settingsItem);
        menu.Items.Add(about);
        menu.Items.Add(quit);

        menu.NeedsUpdate += (_, _) =>
        {
            toggle.Header = islandVisible() ? "Hide Aster" : "Show Aster";
            _start.IsChecked = isAutostart();
            minimal.IsChecked = settings().MinimalNotch;
            mascot.IsChecked = settings().ShowMascot;
            fullscreen.IsChecked = settings().HideInFullscreen;
        };

        var iconPath = OperatingSystem.IsLinux()
            ? "avares://Aster.UI/Assets/tray.png"
            : "avares://Aster.UI/Assets/tray.ico";

        _tray = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri(iconPath))),
            ToolTipText = "Aster Lab Avalonia",
            Menu = menu,
            IsVisible = true
        };

        if (Application.Current is { } app)
        {
            TrayIcon.SetIcons(app, new TrayIcons { _tray });
        }
    }

    public void SetAutostartChecked(bool value) => _start.IsChecked = value;

    public void Dispose() => _tray.Dispose();
}
