using Avalonia.Threading;

namespace Aster.UI.Services;

public sealed class DispatcherUiTimer : IUiTimer
{
    private readonly DispatcherTimer _timer;

    public DispatcherUiTimer(TimeSpan interval)
    {
        _timer = new DispatcherTimer { Interval = interval };
        _timer.Tick += (_, _) => Tick?.Invoke(this, EventArgs.Empty);
    }

    public TimeSpan Interval
    {
        get => _timer.Interval;
        set => _timer.Interval = value;
    }

    public bool IsEnabled => _timer.IsEnabled;
    public event EventHandler? Tick;

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    public void Dispose()
    {
        _timer.Stop();
        Tick = null;
    }
}
