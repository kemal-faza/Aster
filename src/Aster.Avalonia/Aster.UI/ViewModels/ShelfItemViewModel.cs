using System.Globalization;
using Aster.Core.Shelf;
using Avalonia.Media;

namespace Aster.UI.ViewModels;

public sealed class ShelfItemViewModel
{
    private static readonly IBrush MutedBrush =
        new SolidColorBrush(Color.Parse("#5E5E66"));
    private static readonly IBrush MissingBrush =
        new SolidColorBrush(Color.Parse("#FF6B63"));

    public ShelfItemViewModel(ShelfItem item)
    {
        Path = item.Path;
        Name = item.Name;
        BadgeText = GetBadgeText(item);
        var badgeColor = GetBadgeColor(item);
        BadgeForeground = new SolidColorBrush(badgeColor);
        BadgeBackground = new SolidColorBrush(
            Color.FromArgb(0x26, badgeColor.R, badgeColor.G, badgeColor.B));
        SizeLabel = GetSizeLabel(item);
        SizeBrush = item.Exists ? MutedBrush : MissingBrush;
        IsDirectory = item.IsDirectory;
        Exists = item.Exists;
        Opacity = item.Exists ? 1 : 0.55;
        RemoveLabel = $"Remove {item.Name}";
    }

    public string Path { get; }
    public string Name { get; }
    public string BadgeText { get; }
    public IBrush BadgeBackground { get; }
    public IBrush BadgeForeground { get; }
    public IBrush SizeBrush { get; }
    public string SizeLabel { get; }
    public bool IsDirectory { get; }
    public bool Exists { get; }
    public double Opacity { get; }
    public string RemoveLabel { get; }

    private static string GetBadgeText(ShelfItem item)
    {
        if (item.IsDirectory)
        {
            return "DIR";
        }

        if (string.IsNullOrWhiteSpace(item.Extension))
        {
            return "FILE";
        }

        return item.Extension.Length > 4 ? item.Extension[..4] : item.Extension;
    }

    private static Color GetBadgeColor(ShelfItem item)
    {
        if (item.IsDirectory)
        {
            return Color.Parse("#FFB86B");
        }

        return item.Extension switch
        {
            "PNG" or "JPG" or "JPEG" or "GIF" or "WEBP" or "BMP" or "SVG" or "HEIC" =>
                Color.Parse("#7FB8FF"),
            "PDF" => Color.Parse("#FF7A7A"),
            "DOC" or "DOCX" or "TXT" or "MD" or "RTF" or "ODT" =>
                Color.Parse("#E6E6EB"),
            "XLS" or "XLSX" or "CSV" or "ODS" => Color.Parse("#6FE3A5"),
            "PPT" or "PPTX" or "ODP" => Color.Parse("#FFB86B"),
            "ZIP" or "RAR" or "7Z" or "TAR" or "GZ" => Color.Parse("#FFD36B"),
            "MP3" or "WAV" or "FLAC" or "AAC" or "M4A" => Color.Parse("#C08CFF"),
            "MP4" or "MOV" or "MKV" or "AVI" or "WEBM" => Color.Parse("#FF8AB8"),
            _ => Color.Parse("#94949C")
        };
    }

    private static string GetSizeLabel(ShelfItem item)
    {
        if (item.IsDirectory)
        {
            return "Folder";
        }

        if (!item.Exists)
        {
            return "Missing";
        }

        if (item.SizeBytes is not { } length)
        {
            return "—";
        }

        if (length < 1024)
        {
            return length.ToString(CultureInfo.InvariantCulture) + " B";
        }

        if (length < 1024 * 1024)
        {
            return (length / 1024.0).ToString("0", CultureInfo.InvariantCulture) + " KB";
        }

        if (length < 1024L * 1024 * 1024)
        {
            return (length / (1024.0 * 1024))
                .ToString("0.0", CultureInfo.InvariantCulture) + " MB";
        }

        return (length / (1024.0 * 1024 * 1024))
            .ToString("0.0", CultureInfo.InvariantCulture) + " GB";
    }
}
