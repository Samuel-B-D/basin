using Basin.Freedesktop;

namespace Basin.Themes.MsStyles;

public static class MsStylesThemes
{
    public const string Extension = ".msstyles";

    public static IReadOnlyList<string> ThemeRoots()
    {
        var roots = new List<string>();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home))
        {
            roots.Add(System.IO.Path.Combine(home, ".themes"));
        }

        AddDataRoots(roots, "themes");
        return roots;
    }

    public static IReadOnlyList<string> StyleRoots()
    {
        var roots = new List<string>();
        AddDataRoots(roots, "msstyles");
        return roots;
    }

    public static IReadOnlyList<string> WineRoots()
    {
        var roots = new List<string>();
        var prefix = Environment.GetEnvironmentVariable("WINEPREFIX");
        if (string.IsNullOrEmpty(prefix))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            prefix = string.IsNullOrEmpty(home) ? null : System.IO.Path.Combine(home, ".wine");
        }

        if (prefix is not null && Descend(prefix, "drive_c", "windows", "resources", "themes") is { } themes)
        {
            roots.Add(themes);
        }

        return roots;
    }

    public static string? Find(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Contains('/', StringComparison.Ordinal))
        {
            return File.Exists(name) ? name : null;
        }

        foreach (var root in ThemeRoots())
        {
            if (Child(root, name, directory: true) is { } directory && InThemeDirectory(directory, name) is { } path)
            {
                return path;
            }
        }

        foreach (var root in StyleRoots().Concat(WineRoots()))
        {
            if (Child(root, name, directory: true) is { } directory && Child(directory, name + Extension, directory: false) is { } path)
            {
                return path;
            }
        }

        return null;
    }

    public static IReadOnlyList<MsStylesThemeEntry> Available()
    {
        var found = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in ThemeRoots())
        {
            foreach (var directory in Directories(root))
            {
                var name = System.IO.Path.GetFileName(directory);
                if (!found.ContainsKey(name) && InThemeDirectory(directory, name) is { } path)
                {
                    found[name] = path;
                }
            }
        }

        foreach (var root in StyleRoots().Concat(WineRoots()))
        {
            foreach (var directory in Directories(root))
            {
                var name = System.IO.Path.GetFileName(directory);
                if (!found.ContainsKey(name) && Child(directory, name + Extension, directory: false) is { } path)
                {
                    found[name] = path;
                }
            }
        }

        var entries = new List<MsStylesThemeEntry>(found.Count);
        foreach (var (name, path) in found)
        {
            try
            {
                var theme = MsStylesTheme.Load(path);
                entries.Add(new MsStylesThemeEntry(name, path, theme.ColorSchemes, theme.Sizes));
            }
            catch (MsStylesFormatException)
            {
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return entries;
    }

    private static void AddDataRoots(List<string> roots, string subdirectory)
    {
        var home = System.IO.Path.Combine(XdgDirectories.DataHome, subdirectory);
        if (!roots.Contains(home))
        {
            roots.Add(home);
        }

        foreach (var dataDirectory in XdgDirectories.DataDirectories())
        {
            var candidate = System.IO.Path.Combine(dataDirectory, subdirectory);
            if (!roots.Contains(candidate))
            {
                roots.Add(candidate);
            }
        }
    }

    private static string? InThemeDirectory(string directory, string name)
    {
        if (Child(directory, name + Extension, directory: false) is { } named)
        {
            return named;
        }

        string? single = null;
        foreach (var file in Files(directory))
        {
            if (!file.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (single is not null)
            {
                return null;
            }

            single = file;
        }

        return single;
    }

    private static string? Descend(string root, params string[] names)
    {
        var current = root;
        foreach (var name in names)
        {
            if (Child(current, name, directory: true) is not { } next)
            {
                return null;
            }

            current = next;
        }

        return current;
    }

    private static string? Child(string parent, string name, bool directory = false)
    {
        var exact = System.IO.Path.Combine(parent, name);
        if (directory ? Directory.Exists(exact) : File.Exists(exact))
        {
            return exact;
        }

        foreach (var entry in directory ? Directories(parent) : Files(parent))
        {
            if (string.Equals(System.IO.Path.GetFileName(entry), name, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }

    private static IEnumerable<string> Directories(string parent)
    {
        try
        {
            return Directory.Exists(parent) ? Directory.GetDirectories(parent) : [];
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static IEnumerable<string> Files(string parent)
    {
        try
        {
            return Directory.Exists(parent) ? Directory.GetFiles(parent) : [];
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }
}
