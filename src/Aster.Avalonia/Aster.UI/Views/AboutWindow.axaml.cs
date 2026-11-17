using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Aster.Core;
using Aster.Core.Interfaces;

namespace Aster.UI.Views;

public partial class AboutWindow : Window
{
    public AboutWindow() => InitializeComponent();

    public AboutWindow(IAppLog log) : this()
    {
        // Direct assignment keeps the XAML free of reflection bindings, which
        // NativeAOT cannot trim safely.
        VersionText.Text = AppInfo.DisplayVersion;
        DataPathText.Text = AppPaths.BaseDirectory;
        log.Info("About dialog opened.");
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
            return;
        }

        base.OnKeyDown(e);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
