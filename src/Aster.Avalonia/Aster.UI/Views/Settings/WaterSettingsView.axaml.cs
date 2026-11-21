using System.Globalization;
using Aster.Core.Logic;
using Aster.UI.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Aster.UI.Views;

public partial class WaterSettingsView : UserControl
{
    private SettingsViewModel? _viewModel;

    public WaterSettingsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private SettingsViewModel? ViewModel => _viewModel;

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

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SettingsViewModel.WaterIntervalMinutes)
            or nameof(SettingsViewModel.WaterActiveStartHour)
            or nameof(SettingsViewModel.WaterActiveEndHour)
            or nameof(SettingsViewModel.WaterDailyGoal))
        {
            RefreshBoxes();
        }
    }

    private void RefreshBoxes()
    {
        if (ViewModel is null)
        {
            return;
        }

        if (!IntervalBox.IsFocused)
        {
            IntervalBox.Text = ViewModel.WaterIntervalMinutes.ToString(CultureInfo.InvariantCulture);
        }

        if (!StartBox.IsFocused)
        {
            StartBox.Text = ViewModel.WaterActiveStartHour.ToString(CultureInfo.InvariantCulture);
        }

        if (!EndBox.IsFocused)
        {
            EndBox.Text = ViewModel.WaterActiveEndHour.ToString(CultureInfo.InvariantCulture);
        }

        if (!GoalBox.IsFocused)
        {
            GoalBox.Text = ViewModel.WaterDailyGoal.ToString(CultureInfo.InvariantCulture);
        }
    }

    private void OnIntervalLostFocus(object? sender, RoutedEventArgs e) => CommitInterval();
    private void OnStartLostFocus(object? sender, RoutedEventArgs e) => CommitStart();
    private void OnEndLostFocus(object? sender, RoutedEventArgs e) => CommitEnd();
    private void OnGoalLostFocus(object? sender, RoutedEventArgs e) => CommitGoal();

    private void OnIntervalKeyDown(object? sender, KeyEventArgs e) => CommitOnEnter(e, CommitInterval);
    private void OnStartKeyDown(object? sender, KeyEventArgs e) => CommitOnEnter(e, CommitStart);
    private void OnEndKeyDown(object? sender, KeyEventArgs e) => CommitOnEnter(e, CommitEnd);
    private void OnGoalKeyDown(object? sender, KeyEventArgs e) => CommitOnEnter(e, CommitGoal);

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

    private void CommitInterval()
    {
        if (ViewModel is null) return;
        ViewModel.WaterIntervalMinutes = SettingsNumbers.ParseRange(
            IntervalBox.Text, ViewModel.WaterIntervalMinutes, 1, 240);
        IntervalBox.Text = ViewModel.WaterIntervalMinutes.ToString(CultureInfo.InvariantCulture);
    }

    private void CommitStart()
    {
        if (ViewModel is null) return;
        ViewModel.WaterActiveStartHour = SettingsNumbers.ParseRange(
            StartBox.Text, ViewModel.WaterActiveStartHour, 0, 23);
        StartBox.Text = ViewModel.WaterActiveStartHour.ToString(CultureInfo.InvariantCulture);
    }

    private void CommitEnd()
    {
        if (ViewModel is null) return;
        ViewModel.WaterActiveEndHour = SettingsNumbers.ParseRange(
            EndBox.Text, ViewModel.WaterActiveEndHour, 0, 23);
        EndBox.Text = ViewModel.WaterActiveEndHour.ToString(CultureInfo.InvariantCulture);
    }

    private void CommitGoal()
    {
        if (ViewModel is null) return;
        ViewModel.WaterDailyGoal = SettingsNumbers.ParseRange(
            GoalBox.Text, ViewModel.WaterDailyGoal, 1, 20);
        GoalBox.Text = ViewModel.WaterDailyGoal.ToString(CultureInfo.InvariantCulture);
    }
}
