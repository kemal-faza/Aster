using Aster.Core.Interfaces;
using Aster.Core.Models;
using Aster.Core.Water;

namespace Aster.UI.Services;

public enum WaterDrinkResult
{
    Logged,
    GoalReached,
    Failed
}

public sealed class WaterService : IAsyncDisposable
{
    private readonly ISettingsStore _settings;
    private readonly TimeProvider _time;
    private readonly IUiTimer _timer;
    private readonly IAppLog _log;
    private readonly SemaphoreSlim _tickGate = new(1, 1);
    private DateTimeOffset? _nextReminder;
    private bool _suppressed;
    private bool _disposed;
    private Task _pendingWrite = Task.CompletedTask;
    private WaterConfig _lastConfig;

    public event EventHandler? ReminderDue;
    public event EventHandler? StateChanged;

    public WaterService(
        ISettingsStore settings,
        TimeProvider time,
        IUiTimer timer,
        IAppLog log)
    {
        _settings = settings;
        _time = time;
        _timer = timer;
        _log = log;
        _lastConfig = Config;
        _nextReminder = WaterSchedule.ComputeNextReminder(
            _time.GetLocalNow(), _lastConfig, GlassesToday);
        _timer.Tick += OnTimerTick;
        _settings.Changed += OnSettingsChanged;
    }

    public bool Enabled => _settings.Current.WaterReminderEnabled;
    public int GlassesToday => Math.Clamp(_settings.Current.WaterGlassesToday, 0, 99);
    public int Goal => Math.Clamp(_settings.Current.WaterDailyGoal, 1, 20);
    public bool GoalReached => GlassesToday >= Goal;
    public DateTimeOffset? NextReminderAt => _nextReminder;
    public TimeSpan Interval =>
        TimeSpan.FromMinutes(Math.Clamp(_settings.Current.WaterIntervalMinutes, 1, 240));

    public bool IsSuppressed
    {
        get => _suppressed;
        set
        {
            if (_suppressed == value)
            {
                return;
            }

            _suppressed = value;
            if (!value && !_disposed)
            {
                _ = TickAsync();
            }
        }
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await RollOverAsync(ct);
        Reconfigure();
    }

    public void Start() => _timer.Start();

    public async Task TickAsync(CancellationToken ct = default)
    {
        if (_disposed)
        {
            return;
        }

        await _tickGate.WaitAsync(ct);
        try
        {
            if (_disposed)
            {
                return;
            }

            if (await RollOverAsync(ct))
            {
                StateChanged?.Invoke(this, EventArgs.Empty);
            }

            if (!Enabled || GoalReached || _suppressed)
            {
                return;
            }

            var now = _time.GetLocalNow();
            if (!WaterSchedule.IsWithinActiveHours(
                    now,
                    _settings.Current.WaterActiveStartHour,
                    _settings.Current.WaterActiveEndHour))
            {
                _nextReminder = WaterSchedule.NextActiveStart(
                    now,
                    _settings.Current.WaterActiveStartHour,
                    _settings.Current.WaterActiveEndHour) + Interval;
                return;
            }

            if (_nextReminder is null)
            {
                _nextReminder = now + Interval;
                return;
            }

            if (now < _nextReminder)
            {
                return;
            }

            _nextReminder = now + Interval;
            StateChanged?.Invoke(this, EventArgs.Empty);
            ReminderDue?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _log.Error("Water tick failed.", ex);
        }
        finally
        {
            _tickGate.Release();
        }
    }

    public async Task<WaterDrinkResult> LogDrinkAsync(CancellationToken ct = default)
    {
        await RollOverAsync(ct);

        if (GlassesToday >= Goal)
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
            return WaterDrinkResult.GoalReached;
        }

        var incremented = false;
        var update = await _settings.UpdateAsync(settings =>
        {
            var goal = Math.Clamp(settings.WaterDailyGoal, 1, 20);
            if (settings.WaterGlassesToday >= goal)
            {
                return settings;
            }

            incremented = true;
            return settings with
            {
                WaterGlassesToday = Math.Clamp(settings.WaterGlassesToday + 1, 0, 99),
                WaterDate = WaterSchedule.TodayKey(_time.GetLocalNow())
            };
        }, ct);

        if (!update.Succeeded)
        {
            _log.Warn("Water log was not persisted.");
            return WaterDrinkResult.Failed;
        }

        if (!incremented)
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
            return WaterDrinkResult.GoalReached;
        }

        Reconfigure();
        StateChanged?.Invoke(this, EventArgs.Empty);
        return WaterDrinkResult.Logged;
    }

    public async Task ResetTodayAsync(CancellationToken ct = default)
    {
        var update = await _settings.UpdateAsync(settings => settings with
        {
            WaterGlassesToday = 0,
            WaterDate = WaterSchedule.TodayKey(_time.GetLocalNow())
        }, ct);

        if (!update.Succeeded)
        {
            _log.Warn("Water reset was not persisted.");
        }

        Reconfigure();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Preview()
    {
        if (!_disposed)
        {
            ReminderDue?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task<bool> RollOverAsync(CancellationToken ct)
    {
        var today = WaterSchedule.TodayKey(_time.GetLocalNow());
        if (string.Equals(_settings.Current.WaterDate, today, StringComparison.Ordinal))
        {
            return false;
        }

        var update = await _settings.UpdateAsync(settings => settings with
        {
            WaterGlassesToday = 0,
            WaterDate = today
        }, ct);

        if (!update.Succeeded)
        {
            _log.Warn("Water date rollover was not persisted.");
            return false;
        }

        Reconfigure();
        return true;
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (!_disposed)
        {
            _pendingWrite = TickAsync();
        }
    }

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        if (_disposed)
        {
            return;
        }

        var config = Config;
        if (config != _lastConfig)
        {
            _lastConfig = config;
            Reconfigure();
        }

        // Runtime fields (glasses/date) also arrive through Changed; notify the
        // view models without rearming the schedule or writing settings.
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Reconfigure()
    {
        _nextReminder = WaterSchedule.ComputeNextReminder(
            _time.GetLocalNow(), Config, GlassesToday);
    }

    private WaterConfig Config => new(
        _settings.Current.WaterReminderEnabled,
        _settings.Current.WaterIntervalMinutes,
        _settings.Current.WaterActiveStartHour,
        _settings.Current.WaterActiveEndHour,
        _settings.Current.WaterDailyGoal);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _settings.Changed -= OnSettingsChanged;

        try
        {
            await _pendingWrite;
        }
        catch (Exception ex)
        {
            _log.Error("Pending water write failed during shutdown.", ex);
        }

        _timer.Dispose();
        _tickGate.Dispose();
    }
}
