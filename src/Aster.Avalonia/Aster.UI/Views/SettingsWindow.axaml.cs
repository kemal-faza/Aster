using System.Diagnostics;
using Aster.Core;
using Aster.Core.Models;
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
        viewModel.RefreshRequested += OnRefreshRequested;
        Closed += (_, _) => viewModel.RefreshRequested -= OnRefreshRequested;
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

        GeneralPage.IsVisible = page == "General";
        AppearancePage.IsVisible = page == "Appearance";
        LyricsPage.IsVisible = page == "Lyrics";
        FocusPage.IsVisible = page == "Focus";
        WaterPage.IsVisible = page == "Water";
        AboutPage.IsVisible = page == "About";
    }

    private void OnRefreshRequested(object? sender, AppSettings settings)
    {
        ApplyUnlessFocused(AutostartToggle, settings.StartWithWindows);
        ApplyUnlessFocused(HideFullscreenToggle, settings.HideInFullscreen);
        ApplyUnlessFocused(MinimalNotchToggle, settings.MinimalNotch);
        ApplyUnlessFocused(ShowMascotToggle, settings.ShowMascot);
        ApplyUnlessFocused(GlassToggle, settings.GlassBackground);
        ApplyUnlessFocused(BouncyToggle, settings.BouncySpring);
        ApplyUnlessFocused(VolumeToggle, settings.VolumeToast);
        ApplyUnlessFocused(BrightnessToggle, settings.BrightnessToast);
    }

    private static void ApplyUnlessFocused(CheckBox toggle, bool value)
    {
        if (toggle.IsKeyboardFocusWithin)
        {
            return;
        }

        toggle.IsChecked = value;
    }

    private void OnOpenDataFolderClick(object? sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(AppPaths.BaseDirectory) { UseShellExecute = true });
}
