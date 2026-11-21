using Aster.UI.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Aster.UI.Views;

public partial class ShelfView : UserControl
{
    public ShelfView()
    {
        InitializeComponent();
    }

    private ShelfViewModel? ViewModel => DataContext as ShelfViewModel;

    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ShelfItemViewModel item })
        {
            ViewModel?.Remove(item);
            e.Handled = true;
        }
    }

    private void OnClearClick(object? sender, RoutedEventArgs e) =>
        ViewModel?.Clear();

    private void OnCardDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Visual source &&
            source.FindAncestorOfType<Button>(includeSelf: true) is not null)
        {
            return;
        }

        if (sender is Control { DataContext: ShelfItemViewModel item })
        {
            ViewModel?.Open(item);
            e.Handled = true;
        }
    }
}
