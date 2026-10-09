namespace LunaWm.Tests;

public sealed class TempConfig : IDisposable
{
    public const string Quiet = """
        [quick_launch]
        apps = []

        [notifications]
        sound = false
        """;

    public TempConfig(string contents = Quiet)
    {
        Directory = System.IO.Directory.CreateTempSubdirectory("luna-wm-tests-").FullName;
        Path = System.IO.Path.Combine(Directory, "luna-wm.toml");
        File.WriteAllText(Path, contents);
        var data = System.IO.Path.Combine(Directory, "data");
        System.IO.Directory.CreateDirectory(data);
        foreach (var shared in new[] { "vulkan", "glvnd" })
        {
            var source = System.IO.Path.Combine("/usr/share", shared);
            if (System.IO.Directory.Exists(source))
            {
                System.IO.Directory.CreateSymbolicLink(System.IO.Path.Combine(data, shared), source);
            }
        }
    }

    public string Directory { get; }

    public string Path { get; }

    public Dictionary<string, string> Environment => new()
    {
        ["XDG_STATE_HOME"] = System.IO.Path.Combine(Directory, "state"),
        ["XDG_DATA_HOME"] = System.IO.Path.Combine(Directory, "data"),
        ["XDG_DATA_DIRS"] = System.IO.Path.Combine(Directory, "data"),
    };

    public void Dispose()
    {
        try
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
