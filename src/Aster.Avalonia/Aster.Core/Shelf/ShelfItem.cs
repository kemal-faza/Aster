namespace Aster.Core.Shelf;

public sealed record ShelfItem(
    string Path,
    string Name,
    string Extension,
    bool IsDirectory,
    long? SizeBytes,
    bool Exists);
