using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using System7.Avalonia.Rendering;

namespace System7Wm;

internal static class DepthColors
{
    private static readonly Dictionary<(uint, System7ColorDepth), uint> Cache = [];

    public static uint Map(uint argb, System7ColorDepth depth)
    {
        if (depth is System7ColorDepth.Rgb888 or System7ColorDepth.Monochrome)
        {
            return argb;
        }

        if (Cache.TryGetValue((argb, depth), out var mapped))
        {
            return mapped;
        }

        var brush = new ImmutableSolidColorBrush(Color.FromUInt32(argb));
        var result = System7Colors.Map.Convert([brush, depth], typeof(object), null, CultureInfo.InvariantCulture) is ISolidColorBrush solid
            ? solid.Color.ToUInt32()
            : argb;
        Cache[(argb, depth)] = result;
        return result;
    }
}
