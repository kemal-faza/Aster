namespace Aster.UI.Services;

public sealed class TrayCommandHandler
{
    private readonly Action _toggleVisibility;
    private readonly Func<bool> _isAutostart;
    private readonly Func<bool, bool> _setAutostart;
    private readonly Action _openSettings;
    private readonly Action _quit;
    private readonly Action<bool>? _setMinimal;
    private readonly Action<bool>? _setMascot;
    private readonly Action<bool>? _setFullscreen;
    private readonly Action? _showAbout;

    public TrayCommandHandler(
        Action toggleVisibility,
        Func<bool> isAutostart,
        Func<bool, bool> setAutostart,
        Action openSettings,
        Action quit,
        Action<bool>? setMinimal = null,
        Action<bool>? setMascot = null,
        Action<bool>? setFullscreen = null,
        Action? showAbout = null)
    {
        _toggleVisibility = toggleVisibility;
        _isAutostart = isAutostart;
        _setAutostart = setAutostart;
        _openSettings = openSettings;
        _quit = quit;
        _setMinimal = setMinimal;
        _setMascot = setMascot;
        _setFullscreen = setFullscreen;
        _showAbout = showAbout;
    }

    public void ToggleVisibility() => _toggleVisibility();

    public bool ToggleAutostart()
    {
        var next = !_isAutostart();
        return _setAutostart(next) ? next : _isAutostart();
    }

    public void OpenSettings() => _openSettings();
    public void Quit() => _quit();
    public void SetMinimal(bool enabled) => _setMinimal?.Invoke(enabled);
    public void SetMascot(bool enabled) => _setMascot?.Invoke(enabled);
    public void SetFullscreen(bool enabled) => _setFullscreen?.Invoke(enabled);
    public void ShowAbout() => _showAbout?.Invoke();
}
