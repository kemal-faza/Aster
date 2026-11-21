using System.ComponentModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Aster.UI.ViewModels;

namespace Aster.UI.Controls;

public partial class ToastHost : UserControl
{
    private ToastViewModel? _viewModel;
    private int _generation;

    public ToastHost()
    {
        InitializeComponent();
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as ToastViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _ = ApplyVisibilityAsync();
        }
        else
        {
            ToastBorder.IsVisible = false;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ToastViewModel.IsVisible))
        {
            _ = ApplyVisibilityAsync();
        }

        if (e.PropertyName == nameof(ToastViewModel.IsClickable))
        {
            UpdateCursor();
        }
    }

    private async Task ApplyVisibilityAsync()
    {
        var generation = ++_generation;
        if (_viewModel?.IsVisible == true)
        {
            ToastBorder.IsVisible = true;
            ToastBorder.Opacity = 0;
            UpdateCursor();
            await AnimateOpacityAsync(1, 140);
            return;
        }

        if (!ToastBorder.IsVisible)
        {
            return;
        }

        await AnimateOpacityAsync(0, 220);
        if (generation == _generation)
        {
            ToastBorder.IsVisible = false;
        }
    }

    private async Task AnimateOpacityAsync(double target, int milliseconds)
    {
        var animation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(milliseconds),
            FillMode = FillMode.Forward,
            Easing = new CubicEaseOut(),
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(1),
                    Setters = { new Setter(OpacityProperty, target) }
                }
            }
        };

        await animation.RunAsync(ToastBorder);
    }

    private void UpdateCursor()
    {
        ToastBorder.Cursor = _viewModel?.IsClickable == true
            ? new Cursor(StandardCursorType.Hand)
            : Cursor.Default;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel?.IsClickable != true)
        {
            return;
        }

        _viewModel.Invoke();
        e.Handled = true;
    }
}
