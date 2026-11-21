using Avalonia.Controls;
using Avalonia.Media;
using Aster.UI.ViewModels;

namespace Aster.UI.Views;

public partial class MusicView : UserControl
{
    private bool _lyricsOpen;

    public MusicView() => InitializeComponent();

    private void OnLyricsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _lyricsOpen = !_lyricsOpen;
        MusicMedia.Width = _lyricsOpen ? 260 : 464;
        LyricsPane.Width = _lyricsOpen ? 204 : 0;
        if (MusicInfo.RenderTransform is TranslateTransform infoTranslate)
        {
            infoTranslate.X = _lyricsOpen ? -8 : 0;
        }

        ToolTip.SetTip(LyricsButton, _lyricsOpen ? "Hide lyrics" : "Show lyrics");
    }

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
