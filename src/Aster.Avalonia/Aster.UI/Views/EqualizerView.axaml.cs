using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace Aster.UI.Views;

public partial class EqualizerView : UserControl
{
    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<EqualizerView, bool>(nameof(IsPlaying));

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private readonly Random _random = new();
    private readonly double[] _levels = { 0.3, 0.3, 0.3, 0.3, 0.3 };
    private readonly double[] _targets = { 0.3, 0.3, 0.3, 0.3, 0.3 };
    private readonly ScaleTransform[] _scales;

    public EqualizerView()
    {
        InitializeComponent();
        _scales = new[] { Bar1, Bar2, Bar3, Bar4, Bar5 }
            .Select(bar => (ScaleTransform)bar.RenderTransform!)
            .ToArray();
        _timer.Tick += (_, _) => Animate();
        PropertyChanged += (_, e) =>
        {
            if (e.Property == IsPlayingProperty)
            {
                UpdateTimer();
            }
        };
    }

    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    private void UpdateTimer()
    {
        if (IsPlaying)
        {
            _timer.Start();
            return;
        }

        _timer.Stop();
        for (var i = 0; i < _scales.Length; i++)
        {
            _levels[i] = 0.3;
            _targets[i] = 0.3;
            _scales[i].ScaleY = 0.3;
        }
    }

    private void Animate()
    {
        for (var i = 0; i < _scales.Length; i++)
        {
            if (_random.NextDouble() < 0.2)
            {
                _targets[i] = 0.3 + _random.NextDouble() * 0.7;
            }

            _levels[i] += (_targets[i] - _levels[i]) * 0.55;
            _scales[i].ScaleY = _levels[i];
        }
    }
}
