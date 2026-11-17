using Avalonia.Controls;
using Avalonia.Interactivity;
using Aster.Core;
using Aster.Core.Interfaces;

namespace Aster.UI.Views;

public partial class AboutWindow : Window
{
    public AboutWindow() => InitializeComponent();

    public AboutWindow(IAppLog log) : this()
    {
        DataContext = new { Version = "0.12.0-dev", DataPath = AppPaths.BaseDirectory };
        log.Info("About dialog opened.");
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
