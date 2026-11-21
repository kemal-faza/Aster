using CommunityToolkit.Mvvm.ComponentModel;
using Aster.UI.Services;

namespace Aster.UI.ViewModels;

public sealed partial class ToastViewModel : ObservableObject, IDisposable
{
    private readonly IUiTimer _timer;
    private Action? _onClick;
    private bool _disposed;

    [ObservableProperty] private bool _isVisible;
    [ObservableProperty] private string _text = string.Empty;
    [ObservableProperty] private string _glyph = string.Empty;
    [ObservableProperty] private bool _hasGlyph;
    [ObservableProperty] private bool _showDrop;
    [ObservableProperty] private bool _hasProgress;
    [ObservableProperty] private double _progressWidth;
    [ObservableProperty] private bool _isClickable;
    [ObservableProperty] private bool _isGlass = true;

    public ToastViewModel(IUiTimer timer)
    {
        _timer = timer;
        _timer.Tick += OnTimerTick;
    }

    public void Show(
        string? glyph,
        string text,
        double? progress = null,
        TimeSpan? duration = null,
        Action? onClick = null,
        bool drop = false)
    {
        if (_disposed)
        {
            return;
        }

        _timer.Stop();
        _onClick = onClick;
        Text = text;
        Glyph = glyph ?? string.Empty;
        HasGlyph = !string.IsNullOrEmpty(glyph);
        ShowDrop = drop;
        HasProgress = progress.HasValue;
        ProgressWidth = Math.Clamp(progress ?? 0, 0, 1) * 50;
        IsClickable = onClick is not null;
        IsVisible = true;

        _timer.Interval = duration ?? TimeSpan.FromMilliseconds(1700);
        _timer.Start();
    }

    public void Hide()
    {
        _timer.Stop();
        _onClick = null;
        IsClickable = false;
        IsVisible = false;
    }

    public void Invoke()
    {
        if (!IsClickable)
        {
            return;
        }

        var action = _onClick;
        Hide();
        action?.Invoke();
    }

    private void OnTimerTick(object? sender, EventArgs e) => Hide();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _timer.Dispose();
    }
}
