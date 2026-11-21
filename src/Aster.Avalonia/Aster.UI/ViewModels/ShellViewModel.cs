using System.Globalization;
using Aster.Core.Interfaces;
using Aster.Core.Models;
using Aster.UI.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aster.UI.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private readonly IBatteryStatus? _battery;
    private readonly INetworkStatus? _network;
    private readonly MediaViewModel? _media;
    private FocusViewModel? _focus;
    private bool _lastFocusRunning;
    private bool _lastFocusComplete;

    public MediaViewModel? Media => _media;

    public FocusViewModel? Focus
    {
        get => _focus;
        set
        {
            if (ReferenceEquals(_focus, value))
            {
                return;
            }

            if (_focus is not null)
            {
                _focus.PropertyChanged -= OnFocusPropertyChanged;
            }

            _focus = value;
            _lastFocusRunning = value?.IsRunning ?? false;
            _lastFocusComplete = value?.IsComplete ?? false;
            if (_focus is not null)
            {
                _focus.PropertyChanged += OnFocusPropertyChanged;
            }

            OnPropertyChanged();
            NotifyPill();
        }
    }

    public ToastViewModel? Toast { get; init; }
    public LyricsViewModel? Lyrics { get; set; }
    public bool HasMedia => _media?.HasSession == true;
    public bool HasNoMedia => !HasMedia;

    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private double _expansionProgress;
    [ObservableProperty] private string _clockText = "--:--";
    [ObservableProperty] private string _dateText = string.Empty;
    [ObservableProperty] private bool _isMinimal;
    [ObservableProperty] private string _batteryText = "";
    [ObservableProperty] private bool _isWifiConnected;
    [ObservableProperty] private bool _glassBackground = true;
    [ObservableProperty] private PanelMode _panelMode = PanelMode.Music;
    [ObservableProperty] private bool _isSettingsOpen;
    [ObservableProperty] private string _toastText = string.Empty;
    [ObservableProperty] private string _toastGlyph = string.Empty;
    [ObservableProperty] private bool _isToastVisible;
    [ObservableProperty] private WaterViewModel? _water;

    public ShellViewModel(
        IBatteryStatus? battery = null,
        INetworkStatus? network = null,
        MediaViewModel? media = null,
        CapabilityMatrix? capabilities = null)
    {
        Capabilities = capabilities ?? new CapabilityMatrix(
            Capability.Media | Capability.Battery | Capability.Network | Capability.Tray |
            Capability.Autostart | Capability.Fullscreen | Capability.NoActivate |
            Capability.DpiChange | Capability.SingleInstance | Capability.Focus |
            Capability.Water);
        _battery = battery;
        _network = network;
        _media = media;

        if (_battery is not null)
        {
            _battery.Changed += OnBatteryChanged;
            ApplyBattery(_battery.Current);
        }

        if (_network is not null)
        {
            _network.Changed += OnNetworkChanged;
            IsWifiConnected = _network.IsConnected;
        }

        if (_media is not null)
        {
            _media.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MediaViewModel.HasSession))
                {
                    OnPropertyChanged(nameof(HasMedia));
                    OnPropertyChanged(nameof(HasNoMedia));
                    NotifyPill();
                }

                if (e.PropertyName == nameof(MediaViewModel.IsPlaying))
                {
                    OnPropertyChanged(nameof(PlayGlyph));
                }
            };
        }
    }

    public double ExpandedOpacity => Math.Clamp((ExpansionProgress - 0.18) / 0.82, 0, 1);
    public double CollapsedOpacity => Math.Clamp(1 - ExpansionProgress * 3, 0, 1);
    public string WifiText => IsWifiConnected ? "Wi-Fi" : "Off";
    public double WifiOpacity => IsWifiConnected ? 1 : 0.25;

    public int BatteryPercent => _battery?.Current.Percent ?? 0;
    public bool IsCharging => _battery?.Current.IsCharging ?? false;
    public bool HasBattery => _battery?.Current.HasBattery ?? false;
    public string WifiGlyph => Glyph.Get(Glyph.Kind.Wifi);
    public string ChevronGlyph => Glyph.Get(Glyph.Kind.ChevronUp);
    public string PlayGlyph => Media?.IsPlaying == true
        ? Glyph.Get(Glyph.Kind.Pause)
        : Glyph.Get(Glyph.Kind.Play);
    public string PreviousGlyph => Glyph.Get(Glyph.Kind.Previous);
    public string NextGlyph => Glyph.Get(Glyph.Kind.Next);
    public string MusicGlyph => Glyph.Get(Glyph.Kind.Music);
    public string CameraGlyph => Glyph.Get(Glyph.Kind.Camera);
    public string ShelfGlyph => Glyph.Get(Glyph.Kind.Shelf);
    public string FocusGlyph => Glyph.Get(Glyph.Kind.Focus);
    public string SettingsGlyph => Glyph.Get(Glyph.Kind.Settings);
    public string LyricsGlyph => Glyph.Get(Glyph.Kind.Lyrics);

    public bool ShowFocusPill => Focus?.ShowInPill == true;
    public bool IsFocusPaused => Focus?.IsPaused == true;
    public bool ShowClockRow => !ShowFocusPill && HasNoMedia;
    public bool ShowMediaRow => !ShowFocusPill && HasMedia;
    public string PillText => ShowFocusPill ? Focus!.RemainingText : ClockText;
    public bool IsMusicMode => PanelMode == PanelMode.Music;
    public bool IsFocusMode => PanelMode == PanelMode.Focus;

    public CapabilityMatrix Capabilities { get; }

    partial void OnExpansionProgressChanged(double value)
    {
        OnPropertyChanged(nameof(ExpandedOpacity));
        OnPropertyChanged(nameof(CollapsedOpacity));
    }

    partial void OnIsWifiConnectedChanged(bool value)
    {
        OnPropertyChanged(nameof(WifiText));
        OnPropertyChanged(nameof(WifiOpacity));
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (value && Focus?.HasSession == true)
        {
            PanelMode = PanelMode.Focus;
        }
    }

    partial void OnPanelModeChanged(PanelMode value)
    {
        OnPropertyChanged(nameof(IsMusicMode));
        OnPropertyChanged(nameof(IsFocusMode));
    }

    partial void OnClockTextChanged(string value) => NotifyPill();

    public void ApplySettings(AppSettings settings)
    {
        IsMinimal = settings.MinimalNotch;
        GlassBackground = settings.GlassBackground;
        if (Toast is not null)
        {
            Toast.IsGlass = settings.GlassBackground;
        }
    }

    private void OnFocusPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(FocusViewModel.ShowInPill)
            or nameof(FocusViewModel.RemainingText)
            or nameof(FocusViewModel.IsPaused))
        {
            NotifyPill();
        }

        if (e.PropertyName == nameof(FocusViewModel.IsRunning))
        {
            var running = Focus?.IsRunning == true;
            if (running && !_lastFocusRunning)
            {
                PanelMode = PanelMode.Focus;
            }

            _lastFocusRunning = running;
        }

        if (e.PropertyName == nameof(FocusViewModel.IsComplete))
        {
            var complete = Focus?.IsComplete == true;
            if (complete && !_lastFocusComplete)
            {
                PanelMode = PanelMode.Focus;
            }

            _lastFocusComplete = complete;
        }
    }

    private void NotifyPill()
    {
        OnPropertyChanged(nameof(ShowFocusPill));
        OnPropertyChanged(nameof(IsFocusPaused));
        OnPropertyChanged(nameof(ShowClockRow));
        OnPropertyChanged(nameof(ShowMediaRow));
        OnPropertyChanged(nameof(PillText));
    }

    private void OnBatteryChanged(object? sender, BatterySnapshot snapshot) => ApplyBattery(snapshot);
    private void OnNetworkChanged(object? sender, bool connected) => IsWifiConnected = connected;

    private void ApplyBattery(BatterySnapshot snapshot)
    {
        if (!snapshot.HasBattery)
        {
            BatteryText = string.Empty;
            OnPropertyChanged(nameof(BatteryPercent));
            OnPropertyChanged(nameof(IsCharging));
            OnPropertyChanged(nameof(HasBattery));
            return;
        }

        BatteryText = snapshot.IsCharging ? $"{snapshot.Percent}%+" : $"{snapshot.Percent}%";
        OnPropertyChanged(nameof(BatteryPercent));
        OnPropertyChanged(nameof(IsCharging));
        OnPropertyChanged(nameof(HasBattery));
    }

    public void UpdateClock(DateTimeOffset now)
    {
        ClockText = now.ToString("HH:mm", CultureInfo.CurrentCulture);
        var date = now.ToString("ddd, d MMM", CultureInfo.CurrentCulture);
        DateText = date.Length > 0
            ? char.ToUpper(date[0], CultureInfo.CurrentCulture) + date[1..]
            : date;
    }
}
