using System.Xml.Linq;

namespace Basin.Freedesktop;

public static class RecentlyUsed
{
    private static readonly XNamespace Mime = "http://www.freedesktop.org/standards/shared-mime-info";

    public static string DefaultPath => Path.Combine(XdgDirectories.DataHome, "recently-used.xbel");

    public static IReadOnlyList<RecentlyUsedItem> Read(string? path = null)
    {
        path ??= DefaultPath;
        if (!File.Exists(path))
        {
            return [];
        }

        var items = new List<RecentlyUsedItem>();
        foreach (var bookmark in XDocument.Load(path).Root?.Elements("bookmark") ?? [])
        {
            var href = bookmark.Attribute("href")?.Value;
            if (href is null || !href.StartsWith("file://", StringComparison.Ordinal))
            {
                continue;
            }

            var mimeType = bookmark.Descendants(Mime + "mime-type").FirstOrDefault()?.Attribute("type")?.Value;
            var modified = DateTime.TryParse(bookmark.Attribute("modified")?.Value, out var stamp) ? stamp : DateTime.MinValue;
            items.Add(new RecentlyUsedItem(Uri.UnescapeDataString(new Uri(href).LocalPath), mimeType, modified));
        }

        items.Sort(static (a, b) => b.Modified.CompareTo(a.Modified));
        return items;
    }
}
