using Aster.Core.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace Aster.UI.Services;

public sealed class TrayService : IDisposable
{
    private readonly TrayIcon _tray;
    private readonly NativeMenuItem _toggle;
    private readonly NativeMenuItem _start;
    private readonly NativeMenuItem _minimal;
    private readonly NativeMenuItem _mascot;
    private readonly NativeMenuItem _fullscreen;
    private readonly Func<bool> _isAutostart;
    private readonly Func<AppSettings> _settings;
    private readonly Func<bool> _islandVisible;

    public TrayService(
        TrayCommandHandler commands,
        Func<bool> isAutostart,
        Func<AppSettings> settings,
        Func<bool> islandVisible,
        string autostartLabel)
    {
        _isAutostart = isAutostart;
        _settings = settings;
        _islandVisible = islandVisible;

        var menu = new NativeMenu();

        _toggle = new NativeMenuItem("Show/Hide");
        _toggle.Click += (_, _) =>
        {
            commands.ToggleVisibility();
            ApplyState();
        };

        _start = new NativeMenuItem(autostartLabel)
        {
            ToggleType = MenuItemToggleType.CheckBox
        };
        _start.Click += (_, _) =>
        {
            _start.IsChecked = commands.ToggleAutostart();
            ApplyState();
        };

        _minimal = new NativeMenuItem("Minimal notch")
        {
            ToggleType = MenuItemToggleType.CheckBox
        };
        _minimal.Click += (_, _) =>
        {
            var enabled = !_settings().MinimalNotch;
            commands.SetMinimal(enabled);
            ApplyState();
        };

        _mascot = new NativeMenuItem("Show mascot")
        {
            ToggleType = MenuItemToggleType.CheckBox
        };
        _mascot.Click += (_, _) =>
        {
            var enabled = !_settings().ShowMascot;
            commands.SetMascot(enabled);
            ApplyState();
        };

        _fullscreen = new NativeMenuItem("Hide in fullscreen")
        {
            ToggleType = MenuItemToggleType.CheckBox
        };
        _fullscreen.Click += (_, _) =>
        {
            var enabled = !_settings().HideInFullscreen;
            commands.SetFullscreen(enabled);
            ApplyState();
        };

        var settingsItem = new NativeMenuItem("Settings...");
        settingsItem.Click += (_, _) => commands.OpenSettings();

        var about = new NativeMenuItem("About Aster");
        about.Click += (_, _) => commands.ShowAbout();

        var quit = new NativeMenuItem("Exit");
        quit.Click += (_, _) => commands.Quit();

        menu.Items.Add(_toggle);
        menu.Items.Add(_start);
        menu.Items.Add(_minimal);
        menu.Items.Add(_mascot);
        menu.Items.Add(_fullscreen);
        menu.Items.Add(settingsItem);
        menu.Items.Add(about);
        menu.Items.Add(quit);

        menu.NeedsUpdate += (_, _) => ApplyState();
        ApplyState();

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

    public void Refresh() => ApplyState();

    public void SetAutostartChecked(bool value) => _start.IsChecked = value;

    public void Dispose() => _tray.Dispose();

    private void ApplyState()
    {
        _toggle.Header = _islandVisible() ? "Hide Aster" : "Show Aster";
        _start.IsChecked = _isAutostart();

        var settings = _settings();
        _minimal.IsChecked = settings.MinimalNotch;
        _mascot.IsChecked = settings.ShowMascot;
        _fullscreen.IsChecked = settings.HideInFullscreen;
    }
}
