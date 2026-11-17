using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Aster.UI.Views;

public partial class DockView : UserControl
{
    public event EventHandler? CloseRequested;

    public DockView() => InitializeComponent();

    private void OnCollapseClick(object? sender, RoutedEventArgs e) =>
        CloseRequested?.Invoke(this, EventArgs.Empty);
}
