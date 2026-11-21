using System.ComponentModel;
using System.Globalization;
using Aster.Core.Logic;
using Aster.UI.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Aster.UI.Views;

public partial class FocusSettingsView : UserControl
{
    private SettingsViewModel? _viewModel;

    public FocusSettingsView()
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

        _viewModel = DataContext as SettingsViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            RefreshBoxes();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SettingsViewModel.FocusMinutes)
            or nameof(SettingsViewModel.ShortBreakMinutes)
            or nameof(SettingsViewModel.LongBreakMinutes))
        {
            RefreshBoxes();
        }
    }

    private void RefreshBoxes()
    {
        if (_viewModel is null)
        {
            return;
        }

        if (!FocusBox.IsFocused)
        {
            FocusBox.Text = _viewModel.FocusMinutes.ToString(CultureInfo.InvariantCulture);
        }

        if (!ShortBox.IsFocused)
        {
            ShortBox.Text = _viewModel.ShortBreakMinutes.ToString(CultureInfo.InvariantCulture);
        }

        if (!LongBox.IsFocused)
        {
            LongBox.Text = _viewModel.LongBreakMinutes.ToString(CultureInfo.InvariantCulture);
        }
    }

    private void OnFocusLostFocus(object? sender, RoutedEventArgs e) => CommitFocus();
    private void OnShortLostFocus(object? sender, RoutedEventArgs e) => CommitShort();
    private void OnLongLostFocus(object? sender, RoutedEventArgs e) => CommitLong();

    private void OnFocusKeyDown(object? sender, KeyEventArgs e) => CommitOnEnter(e, CommitFocus);
    private void OnShortKeyDown(object? sender, KeyEventArgs e) => CommitOnEnter(e, CommitShort);
    private void OnLongKeyDown(object? sender, KeyEventArgs e) => CommitOnEnter(e, CommitLong);

    private static void CommitOnEnter(KeyEventArgs e, Action commit)
    {
        if (e.Key == Key.Enter)
        {
            commit();
            e.Handled = true;
        }
    }

    private void OnNumericTextInput(object? sender, TextInputEventArgs e)
    {
        if (e.Text?.All(char.IsDigit) != true)
        {
            e.Handled = true;
        }
    }

    private void CommitFocus()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.FocusMinutes = SettingsNumbers.ParseRange(
            FocusBox.Text, _viewModel.FocusMinutes, 1, 180);
        FocusBox.Text = _viewModel.FocusMinutes.ToString(CultureInfo.InvariantCulture);
    }

    private void CommitShort()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.ShortBreakMinutes = SettingsNumbers.ParseRange(
            ShortBox.Text, _viewModel.ShortBreakMinutes, 1, 180);
        ShortBox.Text = _viewModel.ShortBreakMinutes.ToString(CultureInfo.InvariantCulture);
    }

    private void CommitLong()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.LongBreakMinutes = SettingsNumbers.ParseRange(
            LongBox.Text, _viewModel.LongBreakMinutes, 1, 180);
        LongBox.Text = _viewModel.LongBreakMinutes.ToString(CultureInfo.InvariantCulture);
    }
}
