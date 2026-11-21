using System.ComponentModel;
using Aster.Core.Models;
using Aster.UI.ViewModels;
using Avalonia.Controls;

namespace Aster.UI.Views;

public partial class DockView : UserControl
{
    private ShellViewModel? _viewModel;
    private bool _syncing;

    public event EventHandler? SettingsRequested;
    public event EventHandler? CloseRequested;

    public DockView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as ShellViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            SyncChecks();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.PanelMode))
        {
            SyncChecks();
        }
    }

    private void OnTabChecked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_syncing || sender is not RadioButton { IsChecked: true, Tag: string tag })
        {
            return;
        }

        if (_viewModel is not null && Enum.TryParse<PanelMode>(tag, out var mode))
        {
            _viewModel.PanelMode = mode;
        }
    }

    private void SyncChecks()
    {
        if (_viewModel is null)
        {
            return;
        }

        _syncing = true;
        try
        {
            MusicTab.IsChecked = _viewModel.PanelMode == PanelMode.Music;
            CameraTab.IsChecked = _viewModel.PanelMode == PanelMode.Camera;
            ShelfTab.IsChecked = _viewModel.PanelMode == PanelMode.Shelf;
            FocusTab.IsChecked = _viewModel.PanelMode == PanelMode.Focus;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnSettingsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        SettingsRequested?.Invoke(this, EventArgs.Empty);

    private void OnCollapseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        CloseRequested?.Invoke(this, EventArgs.Empty);
}
