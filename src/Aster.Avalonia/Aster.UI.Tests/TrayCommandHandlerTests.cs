using Aster.UI.Services;

namespace Aster.UI.Tests;

public class TrayCommandHandlerTests
{
    [Fact]
    public void ToggleAutostartUpdatesState()
    {
        var enabled = false;
        var handler = new TrayCommandHandler(
            toggleVisibility: () => { },
            isAutostart: () => enabled,
            setAutostart: value => { enabled = value; return true; },
            openSettings: () => { },
            quit: () => { });

        Assert.True(handler.ToggleAutostart());
        Assert.True(enabled);
    }

    [Fact]
    public void FailedAutostartKeepsPreviousState()
    {
        var enabled = false;
        var handler = new TrayCommandHandler(
            toggleVisibility: () => { },
            isAutostart: () => enabled,
            setAutostart: _ => false,
            openSettings: () => { },
            quit: () => { });

        Assert.False(handler.ToggleAutostart());
        Assert.False(enabled);
    }

    [Fact]
    public void SetMinimalCallsCallback()
    {
        bool? received = null;
        var handler = CreateHandler(setMinimal: value => received = value);

        handler.SetMinimal(true);

        Assert.True(received);
    }

    [Fact]
    public void SetMascotCallsCallback()
    {
        bool? received = null;
        var handler = CreateHandler(setMascot: value => received = value);

        handler.SetMascot(false);

        Assert.False(received);
    }

    [Fact]
    public void SetFullscreenCallsCallback()
    {
        bool? received = null;
        var handler = CreateHandler(setFullscreen: value => received = value);

        handler.SetFullscreen(true);

        Assert.True(received);
    }

    [Fact]
    public void ShowAboutCallsCallback()
    {
        var calls = 0;
        var handler = CreateHandler(showAbout: () => calls++);

        handler.ShowAbout();

        Assert.Equal(1, calls);
    }

    [Fact]
    public void OptionalCallbacksMayBeNull()
    {
        var handler = CreateHandler();

        handler.SetMinimal(true);
        handler.SetMascot(true);
        handler.SetFullscreen(true);
        handler.ShowAbout();
    }

    private static TrayCommandHandler CreateHandler(
        Action<bool>? setMinimal = null,
        Action<bool>? setMascot = null,
        Action<bool>? setFullscreen = null,
        Action? showAbout = null) =>
        new(
            toggleVisibility: () => { },
            isAutostart: () => false,
            setAutostart: _ => true,
            openSettings: () => { },
            quit: () => { },
            setMinimal: setMinimal,
            setMascot: setMascot,
            setFullscreen: setFullscreen,
            showAbout: showAbout);
}
