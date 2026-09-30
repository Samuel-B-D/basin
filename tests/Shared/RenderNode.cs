using System.Runtime.InteropServices;

namespace Basin.Tests;

internal static class RenderNode
{
    [DllImport("libc", SetLastError = true)]
    private static extern int setenv(string name, string value, int overwrite);

    public static string Path { get; } = Pick();

    public static string? DriverOf(string node)
    {
        var driver = $"/sys/class/drm/{System.IO.Path.GetFileName(node)}/device/driver";
        return Directory.Exists(driver)
            ? Directory.ResolveLinkTarget(driver, returnFinalTarget: true)?.Name
            : null;
    }

    private static string Pick()
    {
        if (Environment.GetEnvironmentVariable("BASIN_RENDER_NODE") is { Length: > 0 } forced)
        {
            return forced;
        }

        var nodes = Directory.Exists("/dev/dri")
            ? Directory.GetFiles("/dev/dri", "renderD*").OrderBy(p => p, StringComparer.Ordinal).ToArray()
            : [];
        var node = nodes.FirstOrDefault(candidate => DriverOf(candidate) != "nvidia")
            ?? nodes.FirstOrDefault()
            ?? "/dev/dri/renderD128";

        if (DriverOf(node) is not (null or "nvidia"))
        {
            const string icdDir = "/usr/share/vulkan/icd.d";
            if (Directory.Exists(icdDir))
            {
                var icds = string.Join(':', Directory.GetFiles(icdDir, "*.json")
                    .Where(p => !System.IO.Path.GetFileName(p).Contains("nvidia", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(p => p, StringComparer.Ordinal));

                setenv("VK_DRIVER_FILES", icds, 1);
                setenv("VK_ICD_FILENAMES", icds, 1);
            }
        }

        return node;
    }
}
