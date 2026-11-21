using Aster.UI.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;

namespace Aster.UI.Views;

public partial class ShelfView : UserControl
{
    private const double DragThreshold = 4;

    private PointerPressedEventArgs? _dragArgs;
    private ShelfItemViewModel? _dragItem;
    private Point _dragStart;

    public event EventHandler<bool>? DragOutChanged;

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

    private void OnCardPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: ShelfItemViewModel item } card ||
            !item.Exists)
        {
            return;
        }

        if (!e.GetCurrentPoint(card).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (e.Source is Visual source &&
            source.FindAncestorOfType<Button>(includeSelf: true) is not null)
        {
            return;
        }

        _dragItem = item;
        _dragArgs = e;
        _dragStart = e.GetPosition(card);
    }

    private async void OnCardPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragItem is null || _dragArgs is null || sender is not Control card)
        {
            return;
        }

        if (!e.GetCurrentPoint(card).Properties.IsLeftButtonPressed)
        {
            ResetDrag();
            return;
        }

        var position = e.GetPosition(card);
        var dx = position.X - _dragStart.X;
        var dy = position.Y - _dragStart.Y;
        if (dx * dx + dy * dy < DragThreshold * DragThreshold)
        {
            return;
        }

        var item = _dragItem;
        var args = _dragArgs;
        ResetDrag();

        ViewModel?.SetDragging(true);
        DragOutChanged?.Invoke(this, true);
        e.Handled = true;

        try
        {
            var top = TopLevel.GetTopLevel(this);
            if (top is null)
            {
                return;
            }

            IStorageItem? storage = item.IsDirectory
                ? await top.StorageProvider.TryGetFolderFromPathAsync(item.Path)
                : await top.StorageProvider.TryGetFileFromPathAsync(item.Path);

            if (storage is null)
            {
                ViewModel?.ReportDragOutFailure(new InvalidOperationException(
                    $"Storage item unavailable: {item.Path}"));
                return;
            }

            var transfer = new DataTransfer();
            transfer.Add(DataTransferItem.Create(DataFormat.File, storage));

            await DragDrop.DoDragDropAsync(args, transfer, DragDropEffects.Copy);
        }
        catch (Exception ex)
        {
            ViewModel?.ReportDragOutFailure(ex);
        }
        finally
        {
            ViewModel?.SetDragging(false);
            DragOutChanged?.Invoke(this, false);
        }
    }

    private void OnCardPointerReleased(object? sender, PointerReleasedEventArgs e) =>
        ResetDrag();

    private void ResetDrag()
    {
        _dragItem = null;
        _dragArgs = null;
    }
}
