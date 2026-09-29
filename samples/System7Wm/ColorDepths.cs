using System7.Avalonia.Rendering;

namespace System7Wm;

internal static class ColorDepths
{
    public static readonly (string Name, System7ColorDepth Depth, string Label)[] All =
    [
        ("bw", System7ColorDepth.Monochrome, "Black & White"),
        ("4", System7ColorDepth.Indexed2, "4"),
        ("16", System7ColorDepth.Indexed4, "16"),
        ("256", System7ColorDepth.Indexed8, "256"),
        ("thousands", System7ColorDepth.Rgb555, "Thousands"),
        ("millions", System7ColorDepth.Rgb888, "Millions"),
    ];

    public static System7ColorDepth? Parse(string? name)
    {
        foreach (var (key, depth, _) in All)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                return depth;
            }
        }

        return null;
    }

    public static string NameOf(System7ColorDepth depth)
    {
        foreach (var (key, value, _) in All)
        {
            if (value == depth)
            {
                return key;
            }
        }

        return "256";
    }
}
