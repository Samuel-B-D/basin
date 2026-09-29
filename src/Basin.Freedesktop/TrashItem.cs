namespace Basin.Freedesktop;

public sealed record TrashItem(string Name, string OriginalPath, DateTime DeletedAt, string FilesPath, string InfoPath)
{
    public bool IsDirectory => Directory.Exists(FilesPath);
}
