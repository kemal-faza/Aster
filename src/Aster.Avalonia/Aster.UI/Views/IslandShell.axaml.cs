using Avalonia.Controls;

namespace Aster.UI.Views;

public partial class IslandShell : UserControl
{
    public event EventHandler? SettingsRequested;
    public event EventHandler? CloseRequested;

    public IslandShell()
    {
        InitializeComponent();
        DockView.CloseRequested += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        DockView.SettingsRequested += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
    }
}
