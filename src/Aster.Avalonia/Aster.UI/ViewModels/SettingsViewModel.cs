using Aster.Core;
using Aster.Core.Interfaces;
using Aster.Core.Models;
using Aster.UI.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aster.UI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsStore _store;
    private readonly IAppLog _log;
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private AppSettings? _lastSaved;
    private bool _refreshing;

    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private bool _minimalNotch;
    [ObservableProperty] private bool _hideInFullscreen;
    [ObservableProperty] private bool _glassBackground;
    [ObservableProperty] private bool _bouncySpring;
    [ObservableProperty] private bool _showMascot;
    [ObservableProperty] private bool _volumeToast;
    [ObservableProperty] private bool _brightnessToast;
    [ObservableProperty] private bool _lyricsEnabled;
    [ObservableProperty] private string _lyricsHighlightColor = SettingsSchema.DefaultLyricsHighlight;
    [ObservableProperty] private bool _waterReminderEnabled;
    [ObservableProperty] private int _waterIntervalMinutes = 60;
    [ObservableProperty] private int _waterActiveStartHour = 8;
    [ObservableProperty] private int _waterActiveEndHour = 22;
    [ObservableProperty] private int _waterDailyGoal = 8;
    [ObservableProperty] private int _waterGlassesToday;
    [ObservableProperty] private string _waterDate = string.Empty;
    [ObservableProperty] private bool _cameraMirror = true;
    [ObservableProperty] private int _focusMinutes = 25;
    [ObservableProperty] private int _shortBreakMinutes = 5;
    [ObservableProperty] private int _longBreakMinutes = 15;
    [ObservableProperty] private bool _focusChime = true;

    public SettingsViewModel(ISettingsStore store, IAppLog log, string autostartLabel)
    {
        _store = store;
        _log = log;
        AutostartLabel = autostartLabel;
        var current = store.Current;
        _startWithWindows = current.StartWithWindows;
        _minimalNotch = current.MinimalNotch;
        _hideInFullscreen = current.HideInFullscreen;
        _glassBackground = current.GlassBackground;
        _bouncySpring = current.BouncySpring;
        _showMascot = current.ShowMascot;
        _volumeToast = current.VolumeToast;
        _brightnessToast = current.BrightnessToast;
        _lyricsEnabled = current.LyricsEnabled;
        _lyricsHighlightColor = current.LyricsHighlightColor;
        _waterReminderEnabled = current.WaterReminderEnabled;
        _waterIntervalMinutes = current.WaterIntervalMinutes;
        _waterActiveStartHour = current.WaterActiveStartHour;
        _waterActiveEndHour = current.WaterActiveEndHour;
        _waterDailyGoal = current.WaterDailyGoal;
        _waterGlassesToday = current.WaterGlassesToday;
        _waterDate = current.WaterDate;
        _cameraMirror = current.CameraMirror;
        _focusMinutes = current.FocusMinutes;
        _shortBreakMinutes = current.ShortBreakMinutes;
        _longBreakMinutes = current.LongBreakMinutes;
        _focusChime = current.FocusChime;
        _saveTimer.Tick += async (_, _) =>
        {
            _saveTimer.Stop();
            await SaveNowAsync();
        };

        _store.Changed += (_, settings) =>
        {
            if (_lastSaved is not null && settings == _lastSaved)
            {
                return;
            }

            Dispatcher.UIThread.Post(() => RefreshFromStore(settings));
        };
    }

    public string AutostartLabel { get; }
    public bool CanSave => _store.CanSave;
    public string? Warning => _store.LoadWarning;
    public string DataPath => AppPaths.BaseDirectory;
    public string CloseGlyph => Glyph.Get(Glyph.Kind.Close);

    public event EventHandler<AppSettings>? Saved;
    public event EventHandler<AppSettings>? RefreshRequested;

    public async Task FlushAsync()
    {
        _saveTimer.Stop();
        await SaveNowAsync();
    }

    public async Task SaveNowAsync()
    {
        if (!CanSave)
        {
            return;
        }

        var next = _store.Current with
        {
            StartWithWindows = StartWithWindows,
            MinimalNotch = MinimalNotch,
            HideInFullscreen = HideInFullscreen,
            GlassBackground = GlassBackground,
            BouncySpring = BouncySpring,
            ShowMascot = ShowMascot,
            VolumeToast = VolumeToast,
            BrightnessToast = BrightnessToast,
            LyricsEnabled = LyricsEnabled,
            LyricsHighlightColor = LyricsHighlightColor,
            WaterReminderEnabled = WaterReminderEnabled,
            WaterIntervalMinutes = WaterIntervalMinutes,
            WaterActiveStartHour = WaterActiveStartHour,
            WaterActiveEndHour = WaterActiveEndHour,
            WaterDailyGoal = WaterDailyGoal,
            WaterGlassesToday = WaterGlassesToday,
            WaterDate = WaterDate,
            CameraMirror = CameraMirror,
            FocusMinutes = FocusMinutes,
            ShortBreakMinutes = ShortBreakMinutes,
            LongBreakMinutes = LongBreakMinutes,
            FocusChime = FocusChime
        };

        _lastSaved = next;
        await _store.SaveAsync(next);
        Saved?.Invoke(this, next);
    }

    partial void OnStartWithWindowsChanged(bool value) => ScheduleSave();
    partial void OnMinimalNotchChanged(bool value) => ScheduleSave();
    partial void OnHideInFullscreenChanged(bool value) => ScheduleSave();
    partial void OnGlassBackgroundChanged(bool value) => ScheduleSave();
    partial void OnBouncySpringChanged(bool value) => ScheduleSave();
    partial void OnShowMascotChanged(bool value) => ScheduleSave();
    partial void OnVolumeToastChanged(bool value) => ScheduleSave();
    partial void OnBrightnessToastChanged(bool value) => ScheduleSave();
    partial void OnLyricsEnabledChanged(bool value) => ScheduleSave();
    partial void OnLyricsHighlightColorChanged(string value) => ScheduleSave();
    partial void OnWaterReminderEnabledChanged(bool value) => ScheduleSave();
    partial void OnWaterIntervalMinutesChanged(int value) => ScheduleSave();
    partial void OnWaterActiveStartHourChanged(int value) => ScheduleSave();
    partial void OnWaterActiveEndHourChanged(int value) => ScheduleSave();
    partial void OnWaterDailyGoalChanged(int value) => ScheduleSave();
    partial void OnWaterGlassesTodayChanged(int value) => ScheduleSave();
    partial void OnWaterDateChanged(string value) => ScheduleSave();
    partial void OnCameraMirrorChanged(bool value) => ScheduleSave();
    partial void OnFocusMinutesChanged(int value) => ScheduleSave();
    partial void OnShortBreakMinutesChanged(int value) => ScheduleSave();
    partial void OnLongBreakMinutesChanged(int value) => ScheduleSave();
    partial void OnFocusChimeChanged(bool value) => ScheduleSave();

    private void RefreshFromStore(AppSettings settings)
    {
        _refreshing = true;
        try
        {
            StartWithWindows = settings.StartWithWindows;
            MinimalNotch = settings.MinimalNotch;
            HideInFullscreen = settings.HideInFullscreen;
            GlassBackground = settings.GlassBackground;
            BouncySpring = settings.BouncySpring;
            ShowMascot = settings.ShowMascot;
            VolumeToast = settings.VolumeToast;
            BrightnessToast = settings.BrightnessToast;
            LyricsEnabled = settings.LyricsEnabled;
            LyricsHighlightColor = settings.LyricsHighlightColor;
            WaterReminderEnabled = settings.WaterReminderEnabled;
            WaterIntervalMinutes = settings.WaterIntervalMinutes;
            WaterActiveStartHour = settings.WaterActiveStartHour;
            WaterActiveEndHour = settings.WaterActiveEndHour;
            WaterDailyGoal = settings.WaterDailyGoal;
            WaterGlassesToday = settings.WaterGlassesToday;
            WaterDate = settings.WaterDate;
            CameraMirror = settings.CameraMirror;
            FocusMinutes = settings.FocusMinutes;
            ShortBreakMinutes = settings.ShortBreakMinutes;
            LongBreakMinutes = settings.LongBreakMinutes;
            FocusChime = settings.FocusChime;
        }
        finally
        {
            _refreshing = false;
        }

        RefreshRequested?.Invoke(this, settings);
    }

    private void ScheduleSave()
    {
        if (_refreshing || !CanSave)
        {
            return;
        }

        _saveTimer.Stop();
        _saveTimer.Start();
    }
}
