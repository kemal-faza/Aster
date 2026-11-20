using Aster.Core.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

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

    private readonly Border[] _bars;

    public EqualizerView()
    {
        InitializeComponent();
        _bars = new[] { Bar1, Bar2, Bar3, Bar4, Bar5 };
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

    private void ApplyLevels(SpectrumFrame frame)
    {
        Set(0, frame.Bass);
        Set(1, frame.LowMid);
        Set(2, frame.Mid);
        Set(3, frame.HighMid);
        Set(4, frame.Treble);

        void Set(int index, double level)
        {
            if (_bars[index].RenderTransform is ScaleTransform scale)
            {
                scale.ScaleY = IsPlaying ? 0.3 + 0.7 * Math.Clamp(level, 0, 1) : 0.3;
            }
        }
    }
}
