using System.Collections.Generic;
using Aster.Core.Models;
using Aster.UI.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;

namespace Aster.UI.Views;

public partial class IslandShell : UserControl
{
    public event EventHandler? SettingsRequested;
    public event EventHandler? CloseRequested;
    public event EventHandler<bool>? DragOutChanged;
    public event EventHandler? ExpandRequested;

    public IslandShell()
    {
        InitializeComponent();
        DockView.CloseRequested += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        DockView.SettingsRequested += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        ShelfView.DragOutChanged += (_, active) =>
            DragOutChanged?.Invoke(this, active);

        DragDrop.SetAllowDrop(this, true);
        DragDrop.AddDragEnterHandler(this, OnDragEnter);
        DragDrop.AddDragOverHandler(this, OnDragOver);
        DragDrop.AddDragLeaveHandler(this, OnDragLeave);
        DragDrop.AddDropHandler(this, OnDrop);
    }

    private void OnDragEnter(object? sender, DragEventArgs e) =>
        UpdateDropState(e);

    private void OnDragOver(object? sender, DragEventArgs e) =>
        UpdateDropState(e);

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        DropOverlay.IsVisible = false;
        e.Handled = true;
    }

    private void UpdateDropState(DragEventArgs e)
    {
        if (DataContext is not ShellViewModel { HasShelf: true } vm)
        {
            e.DragEffects = DragDropEffects.None;
            DropOverlay.IsVisible = false;
            e.Handled = true;
            return;
        }

        if (!e.DataTransfer.Contains(DataFormat.File))
        {
            e.DragEffects = DragDropEffects.None;
            DropOverlay.IsVisible = false;
            e.Handled = true;
            return;
        }

        ExpandIfNeeded(vm);
        vm.PanelMode = PanelMode.Shelf;
        DropOverlay.IsVisible = true;
        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        DropOverlay.IsVisible = false;

        if (DataContext is not ShellViewModel { HasShelf: true } vm)
        {
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        if (!e.DataTransfer.Contains(DataFormat.File))
        {
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        ExpandIfNeeded(vm);
        vm.PanelMode = PanelMode.Shelf;

        var paths = new List<string>();
        foreach (var item in e.DataTransfer.TryGetFiles() ??
                              System.Array.Empty<IStorageItem>())
        {
            if (item.TryGetLocalPath() is { Length: > 0 } path)
            {
                paths.Add(path);
            }
        }

        if (paths.Count > 0)
        {
            vm.Shelf?.AddPaths(paths);
        }

        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void ExpandIfNeeded(ShellViewModel vm)
    {
        if (!vm.IsExpanded)
        {
            ExpandRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
