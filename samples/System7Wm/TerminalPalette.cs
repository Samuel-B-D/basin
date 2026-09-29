using Avalonia.Controls;
using Avalonia.Media;
using System7.Avalonia.Rendering;

namespace System7Wm;

internal static class TerminalPalette
{
    public const uint Paper = 0xFFFFFFFF;
    public const uint Ink = 0xFF000000;

    public static uint Xterm(int index) => index switch
    {
        0 => 0xFF000000,
        1 => 0xFF800000,
        2 => 0xFF008000,
        3 => 0xFF808000,
        4 => 0xFF000080,
        5 => 0xFF800080,
        6 => 0xFF008080,
        7 => 0xFFC0C0C0,
        8 => 0xFF808080,
        9 => 0xFFFF0000,
        10 => 0xFF00FF00,
        11 => 0xFFFFFF00,
        12 => 0xFF0000FF,
        13 => 0xFFFF00FF,
        14 => 0xFF00FFFF,
        15 => 0xFFFFFFFF,
        >= 16 and <= 231 => Cube(index - 16),
        >= 232 and <= 255 => Gray(index - 232),
        _ => 0xFF000000,
    };

    public static uint Mapped(int index, System7ColorDepth depth) => index switch
    {
        0 => Paper,
        15 => Ink,
        _ when depth == System7ColorDepth.Monochrome => Ink,
        _ => DepthColors.Map(Xterm(index), depth),
    };

    public static string Key(int index) => $"SvcSystems.UI.TerminalColor{index}";

    public static void Apply(IResourceDictionary resources, System7ColorDepth depth)
    {
        for (var i = 0; i < 256; i++)
        {
            resources[Key(i)] = new SolidColorBrush(Mapped(i, depth));
        }
    }

    private static uint Cube(int index)
    {
        static uint Component(int value) => value == 0 ? 0u : (uint)(55 + (value * 40));
        return 0xFF000000 | (Component(index / 36) << 16) | (Component(index / 6 % 6) << 8) | Component(index % 6);
    }

    private static uint Gray(int index)
    {
        var value = (uint)(8 + (index * 10));
        return 0xFF000000 | (value << 16) | (value << 8) | value;
    }
}
