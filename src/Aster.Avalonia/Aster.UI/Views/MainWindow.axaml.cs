using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Threading;
using Aster.Core.Logic;
using Aster.Core.Models;
using Aster.UI.Media;
using Aster.UI.Services;
using Aster.UI.ViewModels;

namespace Aster.UI.Views;

public partial class MainWindow : Window
{
    private readonly AppServices _services;
    private readonly ShellViewModel _state;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly DispatcherTimer _clockTimer;
    private SettingsWindow? _settingsWindow;
    private AboutWindow? _aboutWindow;
    private bool _userHidden;
    private bool _fullscreenHidden;
    private bool _initialized;

    public MainWindow()
    {
        throw new InvalidOperationException("MainWindow requires AppServices.");
    }

    public MainWindow(AppServices services)
    {
        InitializeComponent();
        if (OperatingSystem.IsLinux())
        {
            TransparencyLevelHint = new[]
            {
                WindowTransparencyLevel.Blur,
                WindowTransparencyLevel.Transparent
            };
        }

        _services = services;
        if (services.Shell.Media is { } media)
        {
            media.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MediaViewModel.AccentColor))
                {
                    UpdateEffectLayer();
                }
            };
        }
        _state = services.Shell;
        _settingsViewModel = services.SettingsViewModel;
        DataContext = _state;

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => _state.UpdateClock(DateTimeOffset.Now);
        _clockTimer.Start();

        _services.Fullscreen.Changed += OnFullscreenChanged;
        IslandShell.SettingsRequested += (_, _) => ShowSettings();
        IslandShell.CloseRequested += (_, _) =>
        {
            _services.Motion?.SuppressHover();
            _services.Motion?.SetExpanded(false);
        };
        IslandShell.PointerPressed += (_, _) =>
        {
            if (WindowMotionController.ShouldExpandOnClick(_services.Settings.Current.MinimalNotch))
            {
                _services.Motion?.SetExpanded(true);
            }
        };

        _state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ShellViewModel.ExpansionProgress)
                or nameof(ShellViewModel.IsExpanded)
                or nameof(ShellViewModel.IsMinimal))
            {
                UpdateEffectLayer();
            }
        };

        Screens.Changed += (_, _) =>
        {
            PositionAtTopCenter();
            _services.Motion?.Reapply();
            UpdateEffectLayer();
        };

        _services.AttachWindow(this);
        _services.Chrome.Attach(this);

        Opened += (_, _) =>
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            _services.Log.Info("Aster Lab Avalonia started.");
            _settingsViewModel.Saved += (_, settings) => ApplySettings(settings);
            ApplySettings(_services.Settings.Current);
            if (_services.TrayFailed)
            {
                ShowSettings();
            }

            if (Environment.GetCommandLineArgs().Contains("--settings"))
            {
                ShowSettings();
            }

            PositionAtTopCenter();
            WriteReadyFile();
            _services.EffectLayer.Attach(this);
            UpdateEffectLayer();
            _services.Start();
        };
    }

    public void ToggleVisibility()
    {
        if (IsVisible)
        {
            _userHidden = true;
            Hide();
        }
        else
        {
            _userHidden = false;
            ShowNoActivate();
        }

        UpdateEffectLayer();
    }

    public void SetMinimalNotch(bool enabled)
    {
        _settingsViewModel.MinimalNotch = enabled;
        ApplyIfSettingsReadOnly(_services.Settings.Current with { MinimalNotch = enabled });
    }

    public void SetShowMascot(bool enabled)
    {
        _settingsViewModel.ShowMascot = enabled;
        ApplyIfSettingsReadOnly(_services.Settings.Current with { ShowMascot = enabled });
    }

    public void SetHideInFullscreen(bool enabled)
    {
        _settingsViewModel.HideInFullscreen = enabled;
        ApplyIfSettingsReadOnly(_services.Settings.Current with { HideInFullscreen = enabled });
    }

    private void ApplyIfSettingsReadOnly(AppSettings settings)
    {
        if (!_settingsViewModel.CanSave)
        {
            ApplySettings(settings);
        }
    }

    public void ShowAbout()
    {
        if (_aboutWindow is { IsVisible: true })
        {
            _aboutWindow.Activate();
            return;
        }

        var dialog = new AboutWindow(_services.Log);
        _aboutWindow = dialog;

        var screen = Screens.Primary ?? Screens.All.FirstOrDefault();
        if (screen is not null)
        {
            var width = (int)Math.Round(dialog.Width * screen.Scaling);
            var height = (int)Math.Round(dialog.Height * screen.Scaling);
            var working = screen.WorkingArea;
            var x = working.X + (working.Width - width) / 2;
            var y = working.Y + (working.Height - height) / 2;
            var maxX = Math.Max(working.X, working.Right - width);
            var maxY = Math.Max(working.Y, working.Bottom - height);
            dialog.Position = new PixelPoint(
                Math.Clamp(x, working.X, maxX),
                Math.Clamp(y, working.Y, maxY));
        }

        dialog.Closed += (_, _) =>
        {
            if (ReferenceEquals(_aboutWindow, dialog))
            {
                _aboutWindow = null;
            }
        };

        if (IsVisible)
        {
            dialog.Show(this);
        }
        else
        {
            dialog.Show();
        }
    }

    public void Quit() =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.TryShutdown();

    private void PositionAtTopCenter()
    {
        var handle = TryGetPlatformHandle();
        var screen = Screens.Primary ?? Screens.All.FirstOrDefault();
        if (handle is null || screen is null)
        {
            return;
        }

        var scaling = screen.Scaling;
        var width = (int)Math.Round(Width * scaling);
        var height = (int)Math.Round(Height * scaling);
        var x = WindowGeometry.CenteredLeft(screen.Bounds.X, screen.Bounds.Width, Width, scaling);

        _services.Placement.SetBounds(handle.Handle, x, screen.Bounds.Y, width, height);
    }

    private static void WriteReadyFile()
    {
        var path = Environment.GetEnvironmentVariable("ASTER_READY_FILE");
        if (!string.IsNullOrWhiteSpace(path))
        {
            File.WriteAllText(path, App.StartupClock.ElapsedMilliseconds.ToString());
        }
    }

    private void OnFullscreenChanged(object? sender, bool fullscreen)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_services.Settings.Current.HideInFullscreen)
            {
                if (fullscreen && !_userHidden && !_fullscreenHidden)
                {
                    _fullscreenHidden = true;
                    Hide();
                }
                else if (!fullscreen && _fullscreenHidden)
                {
                    _fullscreenHidden = false;
                    if (!_userHidden)
                    {
                        ShowNoActivate();
                    }
                }
            }

            UpdateEffectLayer();
        });
    }

    private void ShowNoActivate()
    {
        if (!IsVisible)
        {
            Show();
        }

        PositionAtTopCenter();
        UpdateEffectLayer();
    }

    private void ApplySettings(AppSettings settings)
    {
        _state.ApplySettings(settings);
        _services.Motion?.ApplySettings();

        if (!_services.Autostart.SetEnabled(settings.StartWithWindows))
        {
            _services.Log.Warn("Autostart update failed while applying settings.");
            _settingsViewModel.StartWithWindows = _services.Autostart.IsEnabled;
        }

        _services.Tray?.SetAutostartChecked(settings.StartWithWindows);

        if (!settings.HideInFullscreen && _fullscreenHidden)
        {
            _fullscreenHidden = false;
            if (!_userHidden)
            {
                ShowNoActivate();
            }
        }
        else if (settings.HideInFullscreen && _services.Fullscreen.IsFullscreen && !_userHidden && !_fullscreenHidden)
        {
            _fullscreenHidden = true;
            Hide();
        }

        UpdateEffectLayer();
    }

    public void ShowSettings()
    {
        _state.IsSettingsOpen = true;
        UpdateEffectLayer();
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settingsViewModel);
        var screen = Screens.Primary;
        if (screen is not null)
        {
            var width = (int)Math.Round(_settingsWindow.Width * screen.Scaling);
            var height = (int)Math.Round(_settingsWindow.Height * screen.Scaling);
            var working = screen.WorkingArea;
            var x = working.X + (working.Width - width) / 2;
            var y = screen.Bounds.Y + (int)Math.Round((WindowGeometry.ExpandedHeight + 8) * screen.Scaling);
            var maxX = Math.Max(working.X, working.Right - width);
            var maxY = Math.Max(working.Y, working.Bottom - height);
            _settingsWindow.Position = new PixelPoint(
                Math.Clamp(x, working.X, maxX),
                Math.Clamp(y, working.Y, maxY));
        }

        _settingsWindow.Closed += (_, _) =>
        {
            _settingsWindow = null;
            _state.IsSettingsOpen = false;
            UpdateEffectLayer();
        };
        _settingsWindow.Show();
        UpdateEffectLayer();
    }

    private void UpdateEffectLayer()
    {
        var screen = Screens.Primary ?? Screens.All.FirstOrDefault();
        if (screen is null)
        {
            return;
        }

        var scaling = screen.Scaling;
        var pixelWidth = (int)Math.Round(Width * scaling);
        var pixelHeight = (int)Math.Round(Height * scaling);

        // Position is updated asynchronously from ConfigureNotify, so during
        // the spring animation it can still describe the previous frame. The
        // motion controller re-centers the island with this exact formula;
        // using it here keeps the effect window's x constant for every width.
        var islandX = WindowGeometry.CenteredLeft(
            screen.Bounds.X,
            screen.Bounds.Width,
            Width,
            scaling);

        var current = _services.Settings.Current;
        var visibility = new EffectLayerVisibility(
            IslandVisible: IsVisible,
            UserHidden: _userHidden,
            FullscreenHidden: _fullscreenHidden,
            SettingsOpen: _state.IsSettingsOpen,
            GlassEnabled: current.GlassBackground);
        var state = new EffectLayerState(
            Visible: EffectLayerGeometry.IsVisible(visibility),
            Minimal: current.MinimalNotch,
            ExpansionProgress: _state.ExpansionProgress,
            Scaling: scaling,
            IslandX: islandX,
            IslandY: screen.Bounds.Y,
            IslandWidth: pixelWidth,
            IslandHeight: pixelHeight,
            AccentArgb: ToArgb(_services.Shell.Media?.AccentColor ?? ArtworkAccent.Fallback));

        _services.EffectLayer.Update(state);
    }

    private static uint ToArgb(Color color) =>
        0xFF000000u | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
}
