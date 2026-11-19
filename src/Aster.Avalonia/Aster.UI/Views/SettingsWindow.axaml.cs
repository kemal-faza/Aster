using Aster.UI.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Aster.UI.Views;

public partial class SettingsWindow : Window
{
    private bool _ready;

    public SettingsWindow()
    {
        InitializeComponent();
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
            "Lyrics" => ("Lyrics", "Source, highlight color, and local cache."),
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

    private void OnLyricsColorCommitted(object? sender, RoutedEventArgs e) =>
        CommitLyricsColor();

    private void OnLyricsColorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitLyricsColor();
            e.Handled = true;
        }
    }

    private void CommitLyricsColor()
    {
        if (DataContext is not SettingsViewModel vm)
        {
            return;
        }

        vm.TrySetLyricsColor(LyricsColorBox.Text);
        LyricsColorBox.Text = vm.LyricsHighlightColor;
    }

    private void OnLyricsSwatchClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm &&
            sender is Button { Tag: string hex })
        {
            vm.TrySetLyricsColor(hex);
            LyricsColorBox.Text = vm.LyricsHighlightColor;
        }
    }
}
