namespace System7Wm;

internal sealed record FinderEntry(string Path, string Name, bool IsDirectory, long Size, DateTime Modified, string Kind)
{
    public static FinderEntry? Read(string path, Basin.Freedesktop.MimeGlobs mime)
    {
        try
        {
            var info = new FileInfo(path);
            if (info.Attributes.HasFlag(FileAttributes.Directory))
            {
                return new FinderEntry(path, System.IO.Path.GetFileName(path), true, 0, info.LastWriteTime, "folder");
            }

            if (!info.Exists)
            {
                return null;
            }

            var type = mime.TypeOf(path);
            return new FinderEntry(path, info.Name, false, info.Length, info.LastWriteTime, mime.CommentOf(type) ?? type);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
