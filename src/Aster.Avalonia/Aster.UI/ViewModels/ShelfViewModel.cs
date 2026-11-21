using System.Collections.ObjectModel;
using System.Diagnostics;
using Aster.Core.Interfaces;
using Aster.Core.Shelf;
using Aster.UI.Controls;
using Aster.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aster.UI.ViewModels;

public sealed partial class ShelfViewModel : ObservableObject, IDisposable
{
    private readonly ShelfService _service;
    private readonly ToastViewModel? _toast;
    private readonly IUiTimer _timer;
    private readonly IAppLog _log;
    private bool _active;
    private bool _dragging;
    private bool _disposed;

    public ShelfViewModel(
        ShelfService service,
        ToastViewModel? toast,
        IUiTimer timer,
        IAppLog log)
    {
        _service = service;
        _toast = toast;
        _timer = timer;
        _log = log;
        _timer.Tick += OnTimerTick;
        _service.ItemsChanged += OnItemsChanged;
        SyncItems();
    }

    public ObservableCollection<ShelfItemViewModel> Items { get; } = new();
    public bool HasItems => Items.Count > 0;
    public string CountText => $"{Items.Count}/{ShelfService.MaxItems}";
    public string ShelfGlyph => Glyph.Get(Glyph.Kind.Shelf);

    public void AddPaths(IReadOnlyList<string> paths)
    {
        if (_disposed || paths.Count == 0)
        {
            return;
        }

        var result = _service.AddPaths(paths);

        if (result.Added > 0)
        {
            ShowToast(
                result.Added == 1
                    ? "File added to shelf"
                    : $"{result.Added} files added to shelf",
                1800);
        }
        else if (result.Full)
        {
            ShowToast("Shelf is full (8)", 2200);
        }
        else if (result.Rejected > 0)
        {
            ShowToast("Already on shelf", 1800);
        }
    }

    public void Remove(ShelfItemViewModel item)
    {
        if (!_disposed)
        {
            _service.Remove(item.Path);
        }
    }

    public void Clear()
    {
        if (!_disposed)
        {
            _service.Clear();
        }
    }

    public void Open(ShelfItemViewModel item)
    {
        if (_disposed || !item.Exists)
        {
            return;
        }

        try
        {
            ProcessStartInfo startInfo;
            if (OperatingSystem.IsLinux())
            {
                startInfo = new ProcessStartInfo("xdg-open")
                {
                    UseShellExecute = false
                };
                startInfo.ArgumentList.Add(item.Path);
            }
            else
            {
                startInfo = new ProcessStartInfo(item.Path)
                {
                    UseShellExecute = true
                };
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                _log.Warn($"Shelf open produced no process: {item.Path}");
            }
        }
        catch (Exception ex)
        {
            _log.Warn($"Shelf open failed: {ex.Message}");
        }
    }

    public void SetActive(bool active)
    {
        if (_disposed || _active == active)
        {
            return;
        }

        _active = active;

        if (active)
        {
            if (!_dragging)
            {
                _service.RefreshExistence();
            }

            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }

    public void SetDragging(bool dragging) => _dragging = dragging;

    public void ReportDragOutFailure(Exception exception) =>
        _log.Warn($"Shelf drag-out failed: {exception.Message}");

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (!_active || _dragging || _disposed)
        {
            return;
        }

        _service.RefreshExistence();
    }

    private void OnItemsChanged(object? sender, EventArgs e) => SyncItems();

    private void SyncItems()
    {
        Items.Clear();
        foreach (var item in _service.Items)
        {
            Items.Add(new ShelfItemViewModel(item));
        }

        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(CountText));
    }

    private void ShowToast(string text, int durationMs) =>
        _toast?.Show(
            Glyph.Get(Glyph.Kind.Shelf),
            text,
            duration: TimeSpan.FromMilliseconds(durationMs));

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _timer.Dispose();
        _service.ItemsChanged -= OnItemsChanged;
        Items.Clear();
    }
}
