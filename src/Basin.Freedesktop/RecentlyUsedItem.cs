namespace Basin.Freedesktop;

public readonly record struct RecentlyUsedItem(string Path, string? MimeType, DateTime Modified)
{
    public bool IsDirectory => MimeType == "inode/directory";
}
