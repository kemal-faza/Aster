using Avalonia.Controls;
using Aster.UI.ViewModels;

namespace Aster.UI.Views;

public partial class MusicView : UserControl
{
    public MusicView() => InitializeComponent();

    private void OnWaveformSeek(object? sender, double ratio)
    {
        if (DataContext is ShellViewModel { Media: { } media } &&
            media.CanSeek &&
            media.DurationSeconds > 0)
        {
            media.SeekCommand.Execute(ratio * media.DurationSeconds);
        }
    }
}
