namespace Aster.Core.Shelf;

public sealed class ShelfService
{
    public const int MaxItems = 8;

    private readonly List<ShelfItem> _items = new();
    private readonly IReadOnlyList<ShelfItem> _itemsView;

    public ShelfService()
    {
        _itemsView = _items.AsReadOnly();
    }

    public IReadOnlyList<ShelfItem> Items => _itemsView;

    public event EventHandler? ItemsChanged;

    public ShelfAddResult AddPaths(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var added = 0;
        var rejected = 0;
        var full = false;

        foreach (var raw in paths)
        {
            if (_items.Count >= MaxItems)
            {
                full = true;
                rejected++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                rejected++;
                continue;
            }

            string path;
            try
            {
                path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(raw));
            }
            catch
            {
                rejected++;
                continue;
            }

            if (_items.Any(item =>
                    string.Equals(item.Path, path, StringComparison.Ordinal)))
            {
                rejected++;
                continue;
            }

            var isDirectory = Directory.Exists(path);
            var exists = isDirectory || File.Exists(path);
            var size = ReadSize(path, isDirectory, exists);
            var name = GetName(path);
            var extension = isDirectory
                ? string.Empty
                : Path.GetExtension(path).TrimStart('.').ToUpperInvariant();

            _items.Add(new ShelfItem(path, name, extension, isDirectory, size, exists));
            added++;
        }

        if (added > 0)
        {
            ItemsChanged?.Invoke(this, EventArgs.Empty);
        }

        return new ShelfAddResult(added, rejected, full);
    }

    public bool Remove(string path)
    {
        var index = _items.FindIndex(item =>
            string.Equals(item.Path, path, StringComparison.Ordinal));

        if (index < 0)
        {
            return false;
        }

        _items.RemoveAt(index);
        ItemsChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool Clear()
    {
        if (_items.Count == 0)
        {
            return false;
        }

        _items.Clear();
        ItemsChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool RefreshExistence()
    {
        var changed = false;

        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            var isDirectory = Directory.Exists(item.Path);
            var exists = isDirectory || File.Exists(item.Path);
            var size = ReadSize(item.Path, isDirectory, exists);

            if (item.Exists == exists && item.SizeBytes == size)
            {
                continue;
            }

            _items[i] = item with { Exists = exists, SizeBytes = size };
            changed = true;
        }

        if (changed)
        {
            ItemsChanged?.Invoke(this, EventArgs.Empty);
        }

        return changed;
    }

    private static long? ReadSize(string path, bool isDirectory, bool exists)
    {
        if (isDirectory || !exists)
        {
            return null;
        }

        try
        {
            return new FileInfo(path).Length;
        }
        catch
        {
            return null;
        }
    }

    private static string GetName(string path)
    {
        var name = Path.GetFileName(path);
        return string.IsNullOrWhiteSpace(name) ? path : name;
    }
}
