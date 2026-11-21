using System.Globalization;
using Aster.Core.Focus;
using Aster.Core.Interfaces;
using Aster.Core.Models;
using Aster.UI.Abstractions;
using Aster.UI.Controls;
using Aster.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aster.UI.ViewModels;

public sealed partial class FocusViewModel : ObservableObject, IDisposable
{
    private readonly FocusService _focus;
    private readonly ISettingsStore _settings;
    private readonly ToastViewModel? _toast;
    private readonly IChimePlayer? _chime;
    private readonly IAppLog _log;
    private bool _disposed;
    private bool _chimeWarningLogged;

    [ObservableProperty] private string _countdownText = "25:00";
    [ObservableProperty] private double _remainingRatio = 1;
    [ObservableProperty] private string _focusChipText = "Focus 25";
    [ObservableProperty] private string _shortChipText = "Short 5";
    [ObservableProperty] private string _longChipText = "Long 15";
    [ObservableProperty] private bool _isFocusPhase = true;
    [ObservableProperty] private bool _isShortBreakPhase;
    [ObservableProperty] private bool _isLongBreakPhase;
    [ObservableProperty] private string _title = "Focus session";
    [ObservableProperty] private string _ringPhase = "DEEP WORK";
    [ObservableProperty] private string _startLabel = "Start";
    [ObservableProperty] private bool _isStartVisible = true;
    [ObservableProperty] private string _breakLabel = "Start short break";
    [ObservableProperty] private bool _isBreakVisible;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _isPaused;
    [ObservableProperty] private bool _isComplete;
    [ObservableProperty] private bool _showInPill;
    [ObservableProperty] private bool _hasSession;

    public FocusViewModel(
        FocusService focus,
        ISettingsStore settings,
        ToastViewModel? toast,
        IChimePlayer? chime,
        IAppLog log)
    {
        _focus = focus;
        _settings = settings;
        _toast = toast;
        _chime = chime;
        _log = log;

        _focus.Tick += OnTick;
        _focus.StateChanged += OnStateChanged;
        _focus.Completed += OnCompleted;
        RefreshAll();
    }

    public string RemainingText => CountdownText;

    private void OnTick(object? sender, EventArgs e)
    {
        UpdateCountdown();
        RemainingRatio = Ratio();
    }

    private void OnStateChanged(object? sender, EventArgs e) => RefreshAll();

    private void OnCompleted(object? sender, FocusPhase phase)
    {
        if (_settings.Current.FocusChime && _chime is not null)
        {
            try
            {
                _chime.Play();
            }
            catch (Exception ex)
            {
                if (!_chimeWarningLogged)
                {
                    _chimeWarningLogged = true;
                    _log.Warn($"Focus chime failed: {ex.Message}");
                }
            }
        }

        var text = phase == FocusPhase.Focus
            ? "Focus complete · Take a break"
            : "Break over · Back to focus";
        _toast?.Show(
            Glyph.Get(Glyph.Kind.Focus),
            text,
            duration: TimeSpan.FromMilliseconds(3200));
    }

    private void RefreshAll()
    {
        var timer = _focus.Timer;
        var settings = _settings.Current;
        var phase = timer.Phase;

        FocusChipText = $"Focus {Math.Clamp(settings.FocusMinutes, 1, 180)}";
        ShortChipText = $"Short {Math.Clamp(settings.ShortBreakMinutes, 1, 180)}";
        LongChipText = $"Long {Math.Clamp(settings.LongBreakMinutes, 1, 180)}";

        IsFocusPhase = phase == FocusPhase.Focus;
        IsShortBreakPhase = phase == FocusPhase.ShortBreak;
        IsLongBreakPhase = phase == FocusPhase.LongBreak;

        Title = phase switch
        {
            FocusPhase.ShortBreak => "Short break",
            FocusPhase.LongBreak => "Long break",
            _ => "Focus session"
        };
        RingPhase = phase switch
        {
            FocusPhase.ShortBreak => "SHORT BREAK",
            FocusPhase.LongBreak => "LONG BREAK",
            _ => "DEEP WORK"
        };

        StartLabel = timer.IsRunning ? "Pause" : "Start";
        IsStartVisible = !timer.IsComplete;
        IsBreakVisible = timer.IsComplete;
        BreakLabel = timer.IsComplete
            ? phase == FocusPhase.Focus
                ? timer.SuggestBreakPhase() == FocusPhase.LongBreak
                    ? "Start long break"
                    : "Start short break"
                : "Start focus"
            : "Start short break";

        IsRunning = timer.IsRunning;
        IsPaused = timer.IsPaused;
        IsComplete = timer.IsComplete;
        ShowInPill = timer.ShowInPill;
        HasSession = timer.HasSession;

        UpdateCountdown();
        RemainingRatio = Ratio();
    }

    private void UpdateCountdown()
    {
        var remaining = _focus.Timer.Remaining;
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        var text = remaining.TotalHours >= 1
            ? remaining.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : remaining.ToString(@"m\:ss", CultureInfo.InvariantCulture);

        if (CountdownText != text)
        {
            CountdownText = text;
            OnPropertyChanged(nameof(RemainingText));
        }
    }

    private double Ratio()
    {
        var total = _focus.Timer.Duration.TotalMilliseconds;
        return total <= 0
            ? 0
            : Math.Clamp(_focus.Timer.Remaining.TotalMilliseconds / total, 0, 1);
    }

    [RelayCommand]
    private void Toggle() => _focus.ToggleSession();

    [RelayCommand]
    private void Reset() => _focus.ResetSession();

    [RelayCommand]
    private void SelectFocus() => _focus.SelectPhase(FocusPhase.Focus);

    [RelayCommand]
    private void SelectShort() => _focus.SelectPhase(FocusPhase.ShortBreak);

    [RelayCommand]
    private void SelectLong() => _focus.SelectPhase(FocusPhase.LongBreak);

    [RelayCommand]
    private void StartBreak() => _focus.StartSuggestedNext();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _focus.Tick -= OnTick;
        _focus.StateChanged -= OnStateChanged;
        _focus.Completed -= OnCompleted;
    }
}
