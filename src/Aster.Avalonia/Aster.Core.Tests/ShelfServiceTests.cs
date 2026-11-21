using Aster.Core.Shelf;

namespace Aster.Core.Tests;

public sealed class ShelfServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "aster-shelf-" + Guid.NewGuid().ToString("N"));

    public ShelfServiceTests() => Directory.CreateDirectory(_root);

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

    [Fact]
    public void AddsUpToEightDistinctFiles()
    {
        var service = new ShelfService();
        var paths = Enumerable.Range(0, 8)
            .Select(i => PathFor($"file-{i}.txt"))
            .ToArray();

        var result = service.AddPaths(paths);

        Assert.Equal(8, result.Added);
        Assert.Equal(0, result.Rejected);
        Assert.False(result.Full);
        Assert.Equal(8, service.Items.Count);
    }

    [Fact]
    public void RejectsNinthWhenFull()
    {
        var service = new ShelfService();
        service.AddPaths(Enumerable.Range(0, 8).Select(i => PathFor($"file-{i}.txt")));

        var result = service.AddPaths(new[] { PathFor("nine.txt") });

        Assert.Equal(0, result.Added);
        Assert.Equal(1, result.Rejected);
        Assert.True(result.Full);
        Assert.Equal(8, service.Items.Count);
    }

    [Fact]
    public void RejectsDuplicate()
    {
        var service = new ShelfService();
        var path = PathFor("same.txt");
        service.AddPaths(new[] { path });

        var result = service.AddPaths(new[] { path });

        Assert.Equal(0, result.Added);
        Assert.Equal(1, result.Rejected);
        Assert.False(result.Full);
        Assert.Single(service.Items);
    }

    [Fact]
    public void DuplicateComparisonIsCaseSensitive()
    {
        var service = new ShelfService();

        var result = service.AddPaths(new[] { PathFor("a.txt"), PathFor("A.txt") });

        Assert.Equal(2, result.Added);
        Assert.Equal(2, service.Items.Count);
    }

    [Fact]
    public void RejectsWhitespaceAndInvalidPaths()
    {
        var service = new ShelfService();

        var result = service.AddPaths(new[] { "", "   ", "\0" });

        Assert.Equal(0, result.Added);
        Assert.Equal(3, result.Rejected);
        Assert.Empty(service.Items);
    }

    [Fact]
    public void AddsDirectoryWithFolderMetadata()
    {
        var service = new ShelfService();
        var directory = Directory.CreateDirectory(PathFor("folder")).FullName;

        var result = service.AddPaths(new[] { directory });

        Assert.Equal(1, result.Added);
        var item = Assert.Single(service.Items);
        Assert.True(item.IsDirectory);
        Assert.True(item.Exists);
        Assert.Equal(string.Empty, item.Extension);
        Assert.Null(item.SizeBytes);
        Assert.Equal("folder", item.Name);
    }

    [Fact]
    public void AddsMissingFile()
    {
        var service = new ShelfService();

        var result = service.AddPaths(new[] { PathFor("missing.txt") });

        Assert.Equal(1, result.Added);
        var item = Assert.Single(service.Items);
        Assert.False(item.Exists);
        Assert.False(item.IsDirectory);
    }

    [Fact]
    public void TrailingSeparatorDoesNotCreateDuplicate()
    {
        var service = new ShelfService();
        var directory = Directory.CreateDirectory(PathFor("folder")).FullName;
        service.AddPaths(new[] { directory + Path.DirectorySeparatorChar });

        var result = service.AddPaths(new[] { directory });

        Assert.Equal(0, result.Added);
        Assert.Equal(1, result.Rejected);
        Assert.Single(service.Items);
    }

    [Fact]
    public void RemoveUsesOrdinalPath()
    {
        var service = new ShelfService();
        var path = PathFor("remove.txt");
        service.AddPaths(new[] { path });

        Assert.True(service.Remove(path));
        Assert.False(service.Remove(path));
        Assert.Empty(service.Items);
    }

    [Fact]
    public void ClearRemovesEverything()
    {
        var service = new ShelfService();
        service.AddPaths(new[] { PathFor("a.txt"), PathFor("b.txt") });

        Assert.True(service.Clear());
        Assert.False(service.Clear());
        Assert.Empty(service.Items);
    }

    [Fact]
    public void RefreshDetectsFileAppearingAndDisappearing()
    {
        var service = new ShelfService();
        var path = PathFor("later.txt");
        service.AddPaths(new[] { path });
        Assert.False(service.Items[0].Exists);

        File.WriteAllText(path, "new");
        Assert.True(service.RefreshExistence());
        Assert.True(service.Items[0].Exists);

        File.Delete(path);
        Assert.True(service.RefreshExistence());
        Assert.False(service.Items[0].Exists);
    }

    [Fact]
    public void RefreshUpdatesSize()
    {
        var service = new ShelfService();
        var path = CreateFile("size.txt", "x");
        service.AddPaths(new[] { path });
        var before = service.Items[0].SizeBytes;

        File.WriteAllText(path, "a much longer content");
        Assert.True(service.RefreshExistence());

        Assert.NotEqual(before, service.Items[0].SizeBytes);
        Assert.True(service.Items[0].SizeBytes > 1);
    }

    [Fact]
    public void ItemsChangedOnlyFiresForRealChanges()
    {
        var service = new ShelfService();
        var events = 0;
        service.ItemsChanged += (_, _) => events++;
        var path = CreateFile("events.txt", "x");

        service.AddPaths(new[] { path });
        Assert.Equal(1, events);

        Assert.False(service.RefreshExistence());
        Assert.Equal(1, events);

        File.Delete(path);
        Assert.True(service.RefreshExistence());
        Assert.Equal(2, events);
    }
}
