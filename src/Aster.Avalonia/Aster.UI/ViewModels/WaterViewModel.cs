using System.Globalization;
using Aster.Core.Interfaces;
using Aster.Core.Water;
using Aster.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aster.UI.ViewModels;

public sealed partial class WaterViewModel : ObservableObject, IDisposable
{
    private readonly WaterService _service;
    private readonly ToastViewModel? _toast;
    private readonly IAppLog _log;
    private bool _disposed;

    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private int _count;
    [ObservableProperty] private int _goal = 8;
    [ObservableProperty] private string _countText = "0/8";
    [ObservableProperty] private bool _isGoalReached;
    [ObservableProperty] private string _tooltip = "Log a glass of water";
    [ObservableProperty] private string _statusText = "0 of 8 glasses today";
    [ObservableProperty] private string _nextReminderText = string.Empty;
    [ObservableProperty] private IReadOnlyList<WaterBarViewModel> _bars = Array.Empty<WaterBarViewModel>();

    public WaterViewModel(WaterService service, ToastViewModel? toast, IAppLog log)
    {
        _service = service;
        _toast = toast;
        _log = log;
        _service.StateChanged += OnStateChanged;
        _service.ReminderDue += OnReminderDue;
        Refresh();
    }

    private void OnStateChanged(object? sender, EventArgs e) => Refresh();

    private void OnReminderDue(object? sender, EventArgs e)
    {
        if (_service.GoalReached)
        {
            _toast?.Show(
                null,
                $"Goal reached · {_service.Goal}/{_service.Goal}",
                1,
                TimeSpan.FromMilliseconds(2200),
                drop: true);
            return;
        }

        _toast?.Show(
            null,
            $"Time to drink · {_service.GlassesToday}/{_service.Goal}",
            _service.GlassesToday / (double)_service.Goal,
            TimeSpan.FromMilliseconds(6000),
            () => _ = LogAndConfirmAsync(),
            drop: true);
    }

    private void Refresh()
    {
        IsEnabled = _service.Enabled;
        Count = _service.GlassesToday;
        Goal = _service.Goal;
        CountText = $"{Count}/{Goal}";
        IsGoalReached = _service.GoalReached;
        Tooltip = IsGoalReached
            ? $"Goal reached · {Goal} glasses today"
            : $"{Count} of {Goal} glasses today · click to log";

        var filled = WaterSchedule.FilledBars(Count, Goal);
        Bars = Enumerable.Range(0, WaterSchedule.HeaderBarCount)
            .Select(i => new WaterBarViewModel(i < filled, i < filled && IsGoalReached))
            .ToArray();

        var status = $"{Count} of {Goal} glasses today";
        if (!IsEnabled)
        {
            status += " · reminders off";
        }
        else if (IsGoalReached)
        {
            status += " · goal reached";
        }
        else if (_service.NextReminderAt is { } next)
        {
            status += $" · next {next.ToString("HH:mm", CultureInfo.CurrentCulture)}";
        }

        StatusText = status;
        NextReminderText = _service.NextReminderAt?.ToString(
            "HH:mm", CultureInfo.CurrentCulture) ?? string.Empty;
    }

    [RelayCommand]
    private Task LogAsync() => LogAndConfirmAsync();

    [RelayCommand]
    private void Preview() => _service.Preview();

    [RelayCommand]
    private Task ResetAsync() => _service.ResetTodayAsync();

    private async Task LogAndConfirmAsync()
    {
        var result = await _service.LogDrinkAsync();
        switch (result)
        {
            case WaterDrinkResult.Logged:
                _toast?.Show(
                    null,
                    $"Glass logged · {_service.GlassesToday}/{_service.Goal}",
                    _service.GlassesToday / (double)_service.Goal,
                    TimeSpan.FromMilliseconds(1800),
                    drop: true);
                break;
            case WaterDrinkResult.GoalReached:
                _toast?.Show(
                    null,
                    $"Goal reached · {_service.Goal}/{_service.Goal}",
                    1,
                    TimeSpan.FromMilliseconds(1800),
                    drop: true);
                break;
            case WaterDrinkResult.Failed:
                _log.Warn("Water log was not persisted; no toast shown.");
                break;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _service.StateChanged -= OnStateChanged;
        _service.ReminderDue -= OnReminderDue;
    }
}
