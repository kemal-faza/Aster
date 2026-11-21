using System.Diagnostics;
using Aster.UI.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace Aster.UI.Views;

public partial class LyricsView : UserControl
{
    private readonly DispatcherTimer _scrollTimer = new() { Interval = TimeSpan.FromMilliseconds(15) };
    private readonly DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromMilliseconds(30) };
    private LyricsViewModel? _vm;
    private double _scrollFrom;
    private double _scrollTo;
    private long _scrollStart;
    private double _refreshAngle;

    public LyricsView()
    {
        InitializeComponent();
        _scrollTimer.Tick += (_, _) => OnScrollTick();
        _refreshTimer.Tick += (_, _) =>
        {
            _refreshAngle = (_refreshAngle + 12) % 360;
            if (RefreshGlyph.RenderTransform is RotateTransform rotate)
            {
                rotate.Angle = _refreshAngle;
            }
        };
        DataContextChanged += (_, _) => Attach();
    }

    private void Attach()
    {
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _vm = DataContext as LyricsViewModel;
        if (_vm is null)
        {
            return;
        }

        _vm.PropertyChanged += OnViewModelPropertyChanged;
        UpdateRefreshTimer();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LyricsViewModel.ActiveIndex))
        {
            Dispatcher.UIThread.Post(ScrollToActive);
        }
        else if (e.PropertyName == nameof(LyricsViewModel.IsLoading))
        {
            UpdateRefreshTimer();
        }
    }

    private void UpdateRefreshTimer()
    {
        if (_vm?.IsLoading == true)
        {
            _refreshTimer.Start();
        }
        else
        {
            _refreshTimer.Stop();
            _refreshAngle = 0;
            if (RefreshGlyph.RenderTransform is RotateTransform rotate)
            {
                rotate.Angle = 0;
            }
        }
    }

    private void ScrollToActive()
    {
        if (_vm?.ActiveIndex is not { } index || index < 0)
        {
            return;
        }

        if (Lines.ContainerFromIndex(index) is not Control container)
        {
            return;
        }

        var point = container.TranslatePoint(new Point(0, 0), LyricsScroll);
        if (point is null)
        {
            return;
        }

        var target = Math.Max(0,
            LyricsScroll.Offset.Y + point.Value.Y -
            (LyricsScroll.Viewport.Height - container.Bounds.Height) / 2);
        _scrollFrom = LyricsScroll.Offset.Y;
        _scrollTo = target;
        _scrollStart = Stopwatch.GetTimestamp();
        _scrollTimer.Start();
    }

    private void OnScrollTick()
    {
        var elapsed = (Stopwatch.GetTimestamp() - _scrollStart) / (double)Stopwatch.Frequency;
        var t = Math.Min(1, elapsed / 0.22);
        var eased = 1 - Math.Pow(1 - t, 3);
        LyricsScroll.Offset = new Vector(0, _scrollFrom + (_scrollTo - _scrollFrom) * eased);
        if (t >= 1)
        {
            _scrollTimer.Stop();
        }
    }

    private void OnWheel(object? sender, Avalonia.Input.PointerWheelEventArgs e) =>
        _scrollTimer.Stop();
}
