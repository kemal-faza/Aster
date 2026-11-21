using Aster.UI.Services;

namespace Aster.UI.Tests;

internal sealed class ManualUiTimer : IUiTimer
{
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(1);
    public bool IsEnabled { get; private set; }
    public event EventHandler? Tick;

    public void Start() => IsEnabled = true;
    public void Stop() => IsEnabled = false;
    public void Fire() => Tick?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        IsEnabled = false;
        Tick = null;
    }
}
