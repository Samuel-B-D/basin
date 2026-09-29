using Xunit;

namespace System7Wm.Tests;

internal static class Prerequisites
{
    public static string Compositor()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "the compositor is Linux only");
        var compositor = Session.Locate("system7-wm");
        Assert.SkipWhen(compositor is null, "system7-wm has not been built beside the tests");
        return compositor!;
    }

    public static string WlClient(string name)
    {
        var client = Session.WlClient(name);
        Assert.SkipWhen(client is null, $"scripts/wlclients/bin/{name} could not be built");
        return client!;
    }

    public static string? RepoRoot()
    {
        var directory = AppContext.BaseDirectory;
        for (var i = 0; i < 10 && directory is not null; i++)
        {
            if (File.Exists(Path.Combine(directory, "basin.slnx")))
            {
                return directory;
            }

            directory = Path.GetDirectoryName(directory.TrimEnd(Path.DirectorySeparatorChar));
        }

        return null;
    }

    public static string? Built(string project, string binary)
    {
        if (RepoRoot() is not { } root)
        {
            return null;
        }

        var bin = Path.Combine(root, "samples", project, "bin");
        if (!Directory.Exists(bin))
        {
            return null;
        }

        return Directory.EnumerateFiles(bin, binary, SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}publish{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    public static void SkipUnlessRenderer(string renderer)
    {
        if (renderer == "pixman" || renderer == "skia")
        {
            return;
        }

        var node = Environment.GetEnvironmentVariable("BASIN_RENDER_NODE") ?? "/dev/dri/renderD128";
        Assert.SkipUnless(File.Exists(node), $"{renderer} needs a render node");
    }
}
