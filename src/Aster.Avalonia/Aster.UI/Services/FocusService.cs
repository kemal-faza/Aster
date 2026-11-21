using Aster.Core.Focus;
using Aster.Core.Interfaces;
using Aster.Core.Models;

namespace Aster.UI.Services;

public sealed class FocusService : IDisposable
{
    private readonly ISettingsStore _settings;
    private readonly IUiTimer _timer;
    private readonly IAppLog _log;
    private FocusDurations _durations;
    private bool _disposed;

    public FocusTimer Timer { get; }
    public event EventHandler? Tick;
    public event EventHandler? StateChanged;
    public event EventHandler<FocusPhase>? Completed;

    public FocusService(
        ISettingsStore settings,
        TimeProvider time,
        IUiTimer timer,
        IAppLog log)
    {
        _settings = settings;
        _timer = timer;
        _log = log;
        _durations = ReadDurations(settings.Current);
        Timer = new FocusTimer(time, _durations);

        _timer.Tick += OnTimerTick;
        _settings.Changed += OnSettingsChanged;
    }

    public void StartSession()
    {
        Timer.Start();
        SyncTimer();
        RaiseState();
    }

    public void PauseSession()
    {
        Timer.Pause();
        SyncTimer();
        RaiseState();
    }

    public void ToggleSession()
    {
        if (Timer.IsRunning)
        {
            PauseSession();
        }
        else
        {
            StartSession();
        }
    }

    public void ResetSession()
    {
        Timer.Reset();
        SyncTimer();
        RaiseState();
    }

    public void SelectPhase(FocusPhase phase)
    {
        Timer.SetPhase(phase);
        SyncTimer();
        RaiseState();
    }

    public void StartSuggestedNext()
    {
        Timer.StartSuggestedNext();
        SyncTimer();
        RaiseState();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        FocusAdvanceResult result;
        try
        {
            result = Timer.Advance();
        }
        catch (Exception ex)
        {
            _log.Error("Focus timer advance failed.", ex);
            _timer.Stop();
            return;
        }

        if (result.Ticked)
        {
            Tick?.Invoke(this, EventArgs.Empty);
        }

        if (result.Completed)
        {
            _timer.Stop();
            Completed?.Invoke(this, result.Phase);
        }

        if (result.DayChanged || result.Completed)
        {
            RaiseState();
        }
    }

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        if (_disposed)
        {
            return;
        }

        var next = ReadDurations(settings);
        if (next == _durations)
        {
            return;
        }

        _durations = next;
        Timer.UpdateDurations(next);
        RaiseState();
    }

    private void SyncTimer()
    {
        if (Timer.IsRunning)
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }

    private void RaiseState() => StateChanged?.Invoke(this, EventArgs.Empty);

    private static FocusDurations ReadDurations(AppSettings settings) =>
        FocusDurations.FromMinutes(
            settings.FocusMinutes,
            settings.ShortBreakMinutes,
            settings.LongBreakMinutes);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _settings.Changed -= OnSettingsChanged;
        _timer.Dispose();
    }
}
