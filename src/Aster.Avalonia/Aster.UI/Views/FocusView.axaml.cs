using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Aster.UI.ViewModels;

namespace Aster.UI.Views;

public partial class FocusView : UserControl
{
    private const double Center = 75;

    // Ellipse strokes are drawn inside their 144 DIP bounds, so the idle ring
    // has an outer radius of 72. The arc must use the same centerline radius
    // minus half the stroke (72 - 3), otherwise it renders 6 DIP wider when
    // the full ring is replaced by the progress arc.
    private const double Radius = 69;
    private FocusViewModel? _viewModel;

    public FocusView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnPropertyChanged;
        }

        _viewModel = DataContext as FocusViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnPropertyChanged;
            UpdateArc(_viewModel.RemainingRatio);
            UpdatePhasePill(animate: false);
        }
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FocusViewModel.RemainingRatio))
        {
            UpdateArc(_viewModel?.RemainingRatio ?? 0);
            return;
        }

        if (_viewModel is null)
        {
            return;
        }

        var chip = e.PropertyName switch
        {
            nameof(FocusViewModel.IsFocusPhase) when _viewModel.IsFocusPhase => FocusChipButton,
            nameof(FocusViewModel.IsShortBreakPhase) when _viewModel.IsShortBreakPhase => ShortChipButton,
            nameof(FocusViewModel.IsLongBreakPhase) when _viewModel.IsLongBreakPhase => LongChipButton,
            _ => null
        };

        if (chip is not null)
        {
            PhasePill.MoveTo(chip);
        }
    }

    private void UpdatePhasePill(bool animate)
    {
        if (_viewModel is null)
        {
            return;
        }

        var chip = _viewModel.IsFocusPhase
            ? FocusChipButton
            : _viewModel.IsShortBreakPhase
                ? ShortChipButton
                : LongChipButton;
        PhasePill.MoveTo(chip, animate);
    }

    private void UpdateArc(double ratio)
    {
        if (ratio >= 0.999)
        {
            FullRing.IsVisible = true;
            ProgressArc.IsVisible = false;
            return;
        }

        FullRing.IsVisible = false;
        if (ratio <= 0.001)
        {
            ProgressArc.IsVisible = false;
            return;
        }

        ProgressArc.IsVisible = true;
        var start = new Point(Center, Center - Radius);
        var angle = -90 + ratio * 360;
        var radians = angle * Math.PI / 180;
        var end = new Point(
            Center + Radius * Math.Cos(radians),
            Center + Radius * Math.Sin(radians));

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(start, false);
            context.ArcTo(
                end,
                new Size(Radius, Radius),
                0,
                ratio > 0.5,
                SweepDirection.Clockwise,
                true);
        }

        ProgressArc.Data = geometry;
    }
}
