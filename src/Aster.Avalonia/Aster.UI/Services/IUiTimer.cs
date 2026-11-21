namespace Aster.UI.Services;

public interface IUiTimer : IDisposable
{
    TimeSpan Interval { get; set; }
    bool IsEnabled { get; }
    event EventHandler? Tick;
    void Start();
    void Stop();
}
