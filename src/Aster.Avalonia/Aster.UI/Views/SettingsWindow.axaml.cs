using Aster.UI.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Aster.UI.Views;

public partial class SettingsWindow : Window
{
    private bool _ready;
    private SettingsViewModel? _viewModel;

    public SettingsWindow()
    {
        InitializeComponent();
        if (OperatingSystem.IsLinux())
        {
            TransparencyLevelHint = new[]
            {
                WindowTransparencyLevel.Blur,
                WindowTransparencyLevel.Transparent
            };
        }

        DataContextChanged += (_, _) => AttachViewModel();
        Opened += (_, _) => UpdateSurface();
        Closed += (_, _) => DetachViewModel();
        PropertyChanged += (_, e) =>
        {
            if (e.Property == TopLevel.ActualTransparencyLevelProperty)
            {
                UpdateSurface();
            }
        };
        _ready = true;
        ShowPage("General");
    }

    public SettingsWindow(SettingsViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    private void OnTitleBarPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnNavChecked(object? sender, RoutedEventArgs e)
    {
        if (_ready &&
            sender is RadioButton { IsChecked: true, Tag: string page })
        {
            ShowPage(page);
        }
    }

    private void ShowPage(string page)
    {
        if (GeneralPage is null ||
            AppearancePage is null ||
            LyricsPage is null ||
            FocusPage is null ||
            WaterPage is null ||
            AboutPage is null)
        {
            return;
        }

        (PageTitle.Text, PageSubtitle.Text) = page switch
        {
            "Appearance" => ("Appearance", "Glass, motion, and island behavior."),
            "Lyrics" => ("Lyrics", "Source and local cache."),
            "Focus" => ("Focus", "Session lengths and completion sound."),
            "Water" => ("Water", "Reminders, active hours, and daily goal."),
            "About" => ("About", "Data, credits, and app information."),
            _ => ("General", "Startup and fullscreen behavior.")
        };

        GeneralPage.IsVisible = page == "General";
        AppearancePage.IsVisible = page == "Appearance";
        LyricsPage.IsVisible = page == "Lyrics";
        FocusPage.IsVisible = page == "Focus";
        WaterPage.IsVisible = page == "Water";
        AboutPage.IsVisible = page == "About";
    }

    private void OnOpenDataFolderClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel)
        {
            viewModel.OpenDataFolder();
        }
    }

    private void OnClearLyricsCacheClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm && vm.ClearLyricsCache?.Invoke() == true)
        {
            LyricsCacheStatus.Text = "Lyrics cache cleared.";
        }
    }

    private void AttachViewModel()
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as SettingsViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        UpdateSurface();
    }

    private void DetachViewModel()
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }
    }

    private void OnViewModelPropertyChanged(
        object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.GlassBackground))
        {
            UpdateSurface();
        }
    }

    private void UpdateSurface()
    {
        var glass = _viewModel?.GlassBackground == true &&
            ActualTransparencyLevel == WindowTransparencyLevel.Blur;
        var resourceKey = glass ? "SettingsWindowGlassSurface" : "SettingsWindowSurface";
        if (this.TryFindResource(resourceKey, out var resource) && resource is IBrush brush)
        {
            Surface.Background = brush;
        }
    }
}
