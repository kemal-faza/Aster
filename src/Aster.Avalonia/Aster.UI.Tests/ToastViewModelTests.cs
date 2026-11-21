using Aster.UI.ViewModels;

namespace Aster.UI.Tests;

public class ToastViewModelTests
{
    [Fact]
    public void ShowPublishesContentAndStartsTimer()
    {
        var timer = new ManualUiTimer();
        var toast = new ToastViewModel(timer);

        toast.Show("G", "glass logged", 0.5, TimeSpan.FromSeconds(3), () => { }, drop: true);

        Assert.True(toast.IsVisible);
        Assert.Equal("G", toast.Glyph);
        Assert.True(toast.HasGlyph);
        Assert.Equal("glass logged", toast.Text);
        Assert.True(toast.ShowDrop);
        Assert.True(toast.HasProgress);
        Assert.Equal(25, toast.ProgressWidth);
        Assert.True(toast.IsClickable);
        Assert.True(timer.IsEnabled);
    }

    [Fact]
    public void TimerHidesTheToast()
    {
        var timer = new ManualUiTimer();
        var toast = new ToastViewModel(timer);
        toast.Show(null, "hello", duration: TimeSpan.FromMilliseconds(1700));

        timer.Fire();

        Assert.False(toast.IsVisible);
        Assert.False(timer.IsEnabled);
    }

    [Fact]
    public void NewShowReplacesThePreviousDuration()
    {
        var timer = new ManualUiTimer();
        var toast = new ToastViewModel(timer);
        toast.Show(null, "first", duration: TimeSpan.FromSeconds(10));
        toast.Show(null, "second", duration: TimeSpan.FromSeconds(2));

        Assert.Equal(TimeSpan.FromSeconds(2), timer.Interval);
        Assert.Equal("second", toast.Text);
    }

    [Fact]
    public void InvokeHidesThenRunsTheCurrentAction()
    {
        var timer = new ManualUiTimer();
        var toast = new ToastViewModel(timer);
        var calls = 0;
        toast.Show(null, "click", onClick: () => calls++);

        toast.Invoke();

        Assert.Equal(1, calls);
        Assert.False(toast.IsVisible);
    }

    [Fact]
    public void NonClickableToastDoesNotInvoke()
    {
        var toast = new ToastViewModel(new ManualUiTimer());
        toast.Show(null, "plain");

        toast.Invoke();

        Assert.True(toast.IsVisible);
    }

    [Fact]
    public void DefaultDurationIsTheWpfDefault()
    {
        var timer = new ManualUiTimer();
        var toast = new ToastViewModel(timer);

        toast.Show(null, "plain");

        Assert.Equal(TimeSpan.FromMilliseconds(1700), timer.Interval);
    }

    [Fact]
    public void DoubleInvokeRunsTheActionOnce()
    {
        var timer = new ManualUiTimer();
        var toast = new ToastViewModel(timer);
        var calls = 0;
        toast.Show(null, "click", onClick: () => calls++);

        toast.Invoke();
        toast.Invoke();

        Assert.Equal(1, calls);
    }

    [Fact]
    public void HideClearsClickability()
    {
        var toast = new ToastViewModel(new ManualUiTimer());
        var calls = 0;
        toast.Show(null, "click", onClick: () => calls++);

        toast.Hide();
        toast.Invoke();

        Assert.Equal(0, calls);
        Assert.False(toast.IsClickable);
    }

    [Fact]
    public void ProgressIsClampedToTheTrack()
    {
        var toast = new ToastViewModel(new ManualUiTimer());

        toast.Show(null, "a", -1);
        Assert.Equal(0, toast.ProgressWidth);

        toast.Show(null, "b", 2);
        Assert.Equal(50, toast.ProgressWidth);
    }
}
