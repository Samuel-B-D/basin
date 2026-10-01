using Basin.Themes.MsStyles;

namespace Basin.Frames.MsStyles.Quill;

internal static class MsStylesQuillGradients
{
    private const int Length = 256;
    private const int RadialSize = 64;

    public static (int Width, int Height, byte[] Rgba) Rasterize(in MsStylesGradient gradient)
    {
        var count = Math.Max(gradient.Count, 1);
        var stops = count == 1 ? 2 : count;
        Span<float> positions = stackalloc float[stops];
        Span<uint> colors = stackalloc uint[stops];
        var ratios = true;
        for (var i = 0; i < count; i++)
        {
            ratios &= gradient.RatioAt(i) >= 0;
        }

        for (var i = 0; i < stops; i++)
        {
            var index = Math.Min(i, count - 1);
            colors[i] = gradient.ColorAt(index);
            positions[i] = count == 1 ? i : ratios ? gradient.RatioAt(index) / 255f : i / (float)(stops - 1);
        }

        switch (gradient.Kind)
        {
            case FillType.HorizontalGradient:
            {
                var rgba = new byte[Length * 4];
                for (var x = 0; x < Length; x++)
                {
                    Write(rgba, x, Sample(positions, colors, (x + 0.5f) / Length));
                }

                return (Length, 1, rgba);
            }

            case FillType.RadialGradient:
            {
                var rgba = new byte[RadialSize * RadialSize * 4];
                for (var y = 0; y < RadialSize; y++)
                {
                    for (var x = 0; x < RadialSize; x++)
                    {
                        var dx = ((x + 0.5f) / RadialSize) - 0.5f;
                        var dy = ((y + 0.5f) / RadialSize) - 0.5f;
                        var t = MathF.Sqrt((dx * dx) + (dy * dy)) / 0.5f;
                        Write(rgba, (y * RadialSize) + x, Sample(positions, colors, t));
                    }
                }

                return (RadialSize, RadialSize, rgba);
            }

            case FillType.Solid:
            {
                var rgba = new byte[4];
                Write(rgba, 0, colors[0]);
                return (1, 1, rgba);
            }

            default:
            {
                var rgba = new byte[Length * 4];
                for (var y = 0; y < Length; y++)
                {
                    Write(rgba, y, Sample(positions, colors, (y + 0.5f) / Length));
                }

                return (1, Length, rgba);
            }
        }
    }

    private static uint Sample(ReadOnlySpan<float> positions, ReadOnlySpan<uint> colors, float t)
    {
        if (t <= positions[0])
        {
            return colors[0];
        }

        for (var i = 1; i < positions.Length; i++)
        {
            if (t > positions[i])
            {
                continue;
            }

            var span = positions[i] - positions[i - 1];
            var f = span <= 0 ? 1f : (t - positions[i - 1]) / span;
            return Mix(colors[i - 1], colors[i], f);
        }

        return colors[^1];
    }

    private static uint Mix(uint a, uint b, float f)
    {
        uint Channel(int shift) => (uint)Math.Clamp(MathF.Round((((a >> shift) & 0xff) * (1 - f)) + (((b >> shift) & 0xff) * f)), 0, 255) << shift;
        return Channel(24) | Channel(16) | Channel(8) | Channel(0);
    }

    private static void Write(byte[] rgba, int pixel, uint argb)
    {
        var alpha = (argb >> 24) & 0xff;
        var i = pixel * 4;
        rgba[i] = (byte)((((argb >> 16) & 0xff) * alpha + 127) / 255);
        rgba[i + 1] = (byte)((((argb >> 8) & 0xff) * alpha + 127) / 255);
        rgba[i + 2] = (byte)(((argb & 0xff) * alpha + 127) / 255);
        rgba[i + 3] = (byte)alpha;
    }
}
