using Aster.Core.Interfaces;
using Aster.Core.Shelf;
using Aster.UI.Controls;
using Aster.UI.Services;
using Aster.UI.ViewModels;
using Avalonia.Media;

namespace Aster.UI.Tests;

public sealed class ShelfViewModelTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "aster-shelf-vm-" + Guid.NewGuid().ToString("N"));

    public ShelfViewModelTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Temporary cleanup must not fail the test run.
        }
    }

    private string PathFor(string name) => Path.Combine(_root, name);

    private string CreateFile(string name, string content = "x")
    {
        var path = PathFor(name);
        File.WriteAllText(path, content);
        return path;
    }

    private static (ShelfViewModel Vm, ShelfService Service,
        ManualUiTimer Timer, ToastViewModel Toast) Create()
    {
        var service = new ShelfService();
        var timer = new ManualUiTimer();
        var toast = new ToastViewModel(new ManualUiTimer());
        var vm = new ShelfViewModel(service, toast, timer, new NullLog());
        return (vm, service, timer, toast);
    }

    [Fact]
    public void InitialStateIsEmpty()
    {
        var (vm, _, _, _) = Create();

        Assert.Empty(vm.Items);
        Assert.False(vm.HasItems);
        Assert.Equal("0/8", vm.CountText);
        Assert.Equal(Glyph.Get(Glyph.Kind.Shelf), vm.ShelfGlyph);
    }

    [Fact]
    public void AddOneShowsTheSingularToast()
    {
        var (vm, _, _, toast) = Create();

        vm.AddPaths(new[] { PathFor("one.txt") });

        Assert.Single(vm.Items);
        Assert.True(vm.HasItems);
        Assert.Equal("1/8", vm.CountText);
        Assert.Equal("File added to shelf", toast.Text);
        Assert.Equal(Glyph.Get(Glyph.Kind.Shelf), toast.Glyph);
    }

    [Fact]
    public void AddTwoShowsThePluralToast()
    {
        var (vm, _, _, toast) = Create();

        vm.AddPaths(new[] { PathFor("one.txt"), PathFor("two.txt") });

        Assert.Equal(2, vm.Items.Count);
        Assert.Equal("2 files added to shelf", toast.Text);
    }

    [Fact]
    public void NinthItemShowsTheFullToast()
    {
        var (vm, _, _, toast) = Create();

        for (var i = 0; i < 8; i++)
        {
            vm.AddPaths(new[] { PathFor($"file-{i}.txt") });
        }

        vm.AddPaths(new[] { PathFor("nine.txt") });

        Assert.Equal(8, vm.Items.Count);
        Assert.Equal("Shelf is full (8)", toast.Text);
    }

    [Fact]
    public void DuplicateShowsTheDuplicateToast()
    {
        var (vm, _, _, toast) = Create();
        var path = PathFor("same.txt");
        vm.AddPaths(new[] { path });

        vm.AddPaths(new[] { path });

        Assert.Single(vm.Items);
        Assert.Equal("Already on shelf", toast.Text);
    }

    [Fact]
    public void RemoveAndClearUpdateTheCount()
    {
        var (vm, _, _, _) = Create();
        vm.AddPaths(new[] { PathFor("one.txt"), PathFor("two.txt") });

        vm.Remove(vm.Items[0]);

        Assert.Single(vm.Items);
        Assert.Equal("1/8", vm.CountText);

        vm.Clear();

        Assert.Empty(vm.Items);
        Assert.False(vm.HasItems);
        Assert.Equal("0/8", vm.CountText);
    }

    [Fact]
    public void SetActiveControlsTheRefreshTimer()
    {
        var (vm, _, timer, _) = Create();

        vm.SetActive(true);
        Assert.True(timer.IsEnabled);

        vm.SetActive(false);
        Assert.False(timer.IsEnabled);
    }

    [Fact]
    public void TimerRefreshDetectsAMissingFile()
    {
        var (vm, _, timer, _) = Create();
        var path = CreateFile("refresh.txt");
        vm.AddPaths(new[] { path });
        vm.SetActive(true);

        File.Delete(path);
        timer.Fire();

        Assert.False(vm.Items[0].Exists);
        Assert.Equal("Missing", vm.Items[0].SizeLabel);
    }

    [Fact]
    public void DraggingSuppressesTheRefresh()
    {
        var (vm, _, timer, _) = Create();
        var path = CreateFile("hold.txt");
        vm.AddPaths(new[] { path });
        vm.SetActive(true);

        File.Delete(path);
        vm.SetDragging(true);
        timer.Fire();
        Assert.True(vm.Items[0].Exists);

        vm.SetDragging(false);
        timer.Fire();
        Assert.False(vm.Items[0].Exists);
    }

    [Theory]
    [InlineData("PNG", "PNG", "#7FB8FF")]
    [InlineData("PDF", "PDF", "#FF7A7A")]
    [InlineData("ZIP", "ZIP", "#FFD36B")]
    [InlineData("XYZ", "XYZ", "#94949C")]
    public void BadgeUsesTheExtensionAndColor(
        string extension,
        string expectedBadge,
        string expectedColor)
    {
        var item = new ShelfItemViewModel(new ShelfItem(
            "/tmp/file." + extension.ToLowerInvariant(),
            "file." + extension.ToLowerInvariant(),
            extension,
            false,
            1,
            true));

        Assert.Equal(expectedBadge, item.BadgeText);
        var brush = Assert.IsType<SolidColorBrush>(item.BadgeForeground);
        Assert.Equal(Color.Parse(expectedColor), brush.Color);
    }

    [Fact]
    public void DirectoryAndMissingBadgesMatchWpf()
    {
        var directory = new ShelfItemViewModel(new ShelfItem(
            "/tmp/folder", "folder", string.Empty, true, null, true));
        var missing = new ShelfItemViewModel(new ShelfItem(
            "/tmp/missing", "missing", string.Empty, false, null, false));

        Assert.Equal("DIR", directory.BadgeText);
        Assert.Equal("Folder", directory.SizeLabel);
        Assert.Equal("FILE", missing.BadgeText);
        Assert.Equal("Missing", missing.SizeLabel);
        Assert.Equal(0.55, missing.Opacity);
    }

    [Theory]
    [InlineData(512L, "512 B")]
    [InlineData(2048L, "2 KB")]
    [InlineData(1572864L, "1.5 MB")]
    [InlineData(2684354560L, "2.5 GB")]
    public void SizeLabelMatchesWpf(long size, string expected)
    {
        var item = new ShelfItemViewModel(new ShelfItem(
            "/tmp/file.bin", "file.bin", "BIN", false, size, true));

        Assert.Equal(expected, item.SizeLabel);
    }

    [Fact]
    public void ExistingUnreadableSizeUsesTheDashLabel()
    {
        var item = new ShelfItemViewModel(new ShelfItem(
            "/tmp/file.bin", "file.bin", "BIN", false, null, true));

        Assert.Equal("—", item.SizeLabel);
    }

    [Fact]
    public void OpeningAMissingItemIsANoOp()
    {
        var (vm, _, _, _) = Create();
        var item = new ShelfItemViewModel(new ShelfItem(
            "/tmp/missing", "missing", string.Empty, false, null, false));

        vm.Open(item);
    }

    [Fact]
    public void SetActiveRefreshesImmediately()
    {
        var (vm, _, _, _) = Create();
        var path = PathFor("appears.txt");
        vm.AddPaths(new[] { path });
        Assert.False(vm.Items[0].Exists);

        File.WriteAllText(path, "now");
        vm.SetActive(true);

        Assert.True(vm.Items[0].Exists);
    }

    [Fact]
    public void DisposeUnsubscribesFromTheService()
    {
        var (vm, service, _, _) = Create();
        vm.Dispose();

        service.AddPaths(new[] { PathFor("late.txt") });

        Assert.Empty(vm.Items);
    }

    [Fact]
    public void DisposeStopsTheTimer()
    {
        var (vm, _, timer, _) = Create();
        vm.SetActive(true);

        vm.Dispose();

        Assert.False(timer.IsEnabled);
    }

    private sealed class NullLog : IAppLog
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }
}
