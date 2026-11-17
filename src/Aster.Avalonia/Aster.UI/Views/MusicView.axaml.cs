using Avalonia.Controls;
using Avalonia.Interactivity;
using Aster.UI.ViewModels;

namespace Aster.UI.Views;

public partial class MusicView : UserControl
{
    public event EventHandler? SettingsRequested;

    public MusicView() => InitializeComponent();

    private void OnSettingsClick(object? sender, RoutedEventArgs e) =>
        SettingsRequested?.Invoke(this, EventArgs.Empty);

    private void OnSeekStarted(object? sender, EventArgs e)
    {
        if (DataContext is ShellViewModel { Media: not null } vm)
        {
            vm.Media.IsSeeking = true;
        }
    }

    private void OnSeekCompleted(object? sender, double value)
    {
        if (DataContext is ShellViewModel { Media: not null } vm)
        {
            vm.Media.IsSeeking = false;
            if (vm.Media.DurationSeconds > 0)
            {
                vm.Media.SeekCommand.Execute(value);
            }
        }
    }
}
