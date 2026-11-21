using Aster.Core.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace Aster.UI.Views;

public partial class EqualizerView : UserControl
{
    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<EqualizerView, bool>(nameof(IsPlaying));

    public static readonly StyledProperty<IBrush> AccentBrushProperty =
        AvaloniaProperty.Register<EqualizerView, IBrush>(
            nameof(AccentBrush), new SolidColorBrush(Color.Parse("#F6A6C8")));

    public static readonly StyledProperty<SpectrumFrame> SpectrumProperty =
        AvaloniaProperty.Register<EqualizerView, SpectrumFrame>(nameof(Spectrum));

    private const double MinScale = 0.12;
    private const double PausedScale = 0.3;
    private const double LevelCurve = 1.8;
    private const double SpringStiffness = 260;
    private const double SpringDamping = 6;
    private const double SpringFrameSeconds = 1.0 / 30.0;
    private const double MinSpringScale = 0.06;
    private const double MaxSpringScale = 1.25;

    private readonly Border[] _bars;
    private readonly double[] _targets = new double[5];
    private readonly double[] _scales = new double[5];
    private readonly double[] _velocities = new double[5];
    private readonly DispatcherTimer _springTimer =
        new() { Interval = TimeSpan.FromMilliseconds(33) };

    public EqualizerView()
    {
        InitializeComponent();
        _bars = new[] { Bar1, Bar2, Bar3, Bar4, Bar5 };
        Array.Fill(_targets, PausedScale);
        Array.Fill(_scales, PausedScale);
        _springTimer.Tick += (_, _) => AdvanceSpring();
        DetachedFromVisualTree += (_, _) => _springTimer.Stop();
        PropertyChanged += (_, e) =>
        {
            if (e.Property == IsPlayingProperty)
            {
                ApplyLevels(IsPlaying ? Spectrum : SpectrumFrame.Silence);
            }
            else if (e.Property == SpectrumProperty)
            {
                ApplyLevels(IsPlaying ? Spectrum : SpectrumFrame.Silence);
            }
            else if (e.Property == AccentBrushProperty)
            {
                foreach (var bar in _bars)
                {
                    bar.Background = AccentBrush;
                }
            }
        };
    }

    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public IBrush AccentBrush
    {
        get => GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    public SpectrumFrame Spectrum
    {
        get => GetValue(SpectrumProperty);
        set => SetValue(SpectrumProperty, value);
    }

    /// <summary>
    /// Maps a normalized spectrum level to the bar scale. The power curve
    /// widens the loud end of the range: typical music (0.7–0.9) now moves
    /// roughly twice as far as the old linear 0.3–1.0 mapping.
    /// </summary>
    internal static double ScaleFor(double level)
    {
        var normalized = Math.Clamp(level, 0, 1);
        return MinScale + (1.0 - MinScale) * Math.Pow(normalized, LevelCurve);
    }

    /// <summary>
    /// One 30 fps step of an underdamped spring. The scale overshoots its
    /// target before settling, which gives the equalizer its bounce.
    /// </summary>
    internal static (double Scale, double Velocity) SpringStep(
        double scale, double velocity, double target)
    {
        var acceleration = (target - scale) * SpringStiffness - velocity * SpringDamping;
        velocity += acceleration * SpringFrameSeconds;
        scale = Math.Clamp(
            scale + velocity * SpringFrameSeconds, MinSpringScale, MaxSpringScale);
        return (scale, velocity);
    }

    private void ApplyLevels(SpectrumFrame frame)
    {
        if (!IsPlaying)
        {
            _springTimer.Stop();
            Array.Fill(_scales, PausedScale);
            Array.Fill(_targets, PausedScale);
            Array.Clear(_velocities);
            ApplyScales();
            return;
        }

        _targets[0] = ScaleFor(frame.Bass);
        _targets[1] = ScaleFor(frame.LowMid);
        _targets[2] = ScaleFor(frame.Mid);
        _targets[3] = ScaleFor(frame.HighMid);
        _targets[4] = ScaleFor(frame.Treble);
        if (!_springTimer.IsEnabled)
        {
            _springTimer.Start();
        }
    }

    private void AdvanceSpring()
    {
        var moving = false;
        for (var i = 0; i < _bars.Length; i++)
        {
            var (scale, velocity) = SpringStep(_scales[i], _velocities[i], _targets[i]);
            _scales[i] = scale;
            _velocities[i] = velocity;
            if (Math.Abs(scale - _targets[i]) > 0.002 || Math.Abs(velocity) > 0.002)
            {
                moving = true;
            }
        }

        ApplyScales();
        if (!moving)
        {
            _springTimer.Stop();
        }
    }

    private void ApplyScales()
    {
        for (var i = 0; i < _bars.Length; i++)
        {
            if (_bars[i].RenderTransform is ScaleTransform scale)
            {
                scale.ScaleY = _scales[i];
            }
        }
    }
}
