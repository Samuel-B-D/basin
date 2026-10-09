namespace LunaWm;

internal static class DocPath
{
    private static readonly string[] Keys = ["X-DocPath", "X-GNOME-DocPath", "X-KDE-DocPath", "X-MATE-DocPath"];

    public static string? UriFor(string desktopFile)
    {
        try
        {
            return File.Exists(desktopFile) ? UriFrom(File.ReadLines(desktopFile)) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static string? UriFrom(IEnumerable<string> lines)
    {
        var inEntry = false;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                inEntry = line == "[Desktop Entry]";
                continue;
            }

            if (!inEntry || line.StartsWith('#'))
            {
                continue;
            }

            var equals = line.IndexOf('=');
            if (equals <= 0)
            {
                continue;
            }

            var key = line[..equals].Trim();
            var value = line[(equals + 1)..].Trim();
            if (value.Length == 0 || Array.IndexOf(Keys, key) < 0)
            {
                continue;
            }

            if (value.Contains("://", StringComparison.Ordinal) || value.StartsWith("help:", StringComparison.Ordinal) || value.StartsWith("ghelp:", StringComparison.Ordinal))
            {
                return value;
            }

            return key == "X-DocPath" || key == "X-KDE-DocPath"
                ? $"help:/{value.TrimStart('/')}"
                : $"help:{value.Split('/', StringSplitOptions.RemoveEmptyEntries)[0]}";
        }

        return null;
    }
}
