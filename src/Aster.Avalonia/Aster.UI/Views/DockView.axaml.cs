using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Aster.UI.Views;

public partial class DockView : UserControl
{
    public event EventHandler? SettingsRequested;
    public event EventHandler? CloseRequested;

    public DockView() => InitializeComponent();

    private void OnSettingsClick(object? sender, RoutedEventArgs e) =>
        SettingsRequested?.Invoke(this, EventArgs.Empty);

    private void OnCollapseClick(object? sender, RoutedEventArgs e) =>
        CloseRequested?.Invoke(this, EventArgs.Empty);
}
