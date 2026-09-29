using Basin.Scene;

namespace Basin.Effects;

internal static class GridQuad
{
    public const int SegmentWidth = 8;

    public static int Vertices(bool clipLine, bool halo) => (clipLine ? GridRevealMask.LineVertices : 6) * (halo ? 2 : 1);

    public static int Write(
        Span<MeshVertex> into, in GridRevealMask mask, bool clipLine, (float X, float Y) from, (float X, float Y) to,
        float nx, float ny, bool vertical, in GridTone tone, float fromDensity, float toDensity)
    {
        tone.Tones(from.X, from.Y, fromDensity, out var fromColor, out var fromGlow);
        tone.Tones(to.X, to.Y, toDensity, out var toColor, out var toGlow);
        var fromOffset = (from.X + nx, from.Y + ny);
        var toOffset = (to.X + nx, to.Y + ny);
        var clips = clipLine && mask.Clips;
        if (!tone.HaloPart)
        {
            if (clips)
            {
                mask.WriteLine(into[..GridRevealMask.LineVertices], from, to, nx, ny, vertical, fromColor, toColor);
                return GridRevealMask.LineVertices;
            }

            Cell(into[..6], mask, vertical, from, to, toOffset, fromOffset, fromColor, toColor, toColor, fromColor);
            return 6;
        }

        var width = tone.HaloWidth;
        var wx = nx * width;
        var wy = ny * width;
        var clear = RenderColor.Transparent;
        if (clips)
        {
            var piece = GridRevealMask.LineVertices;
            mask.WriteLine(into[..piece], (from.X - wx, from.Y - wy), (to.X - wx, to.Y - wy), wx, wy, vertical, clear, clear);
            Recolor(into[..piece], mask, from, to, vertical, fromGlow, toGlow, outer: true);
            mask.WriteLine(into.Slice(piece, piece), fromOffset, toOffset, wx, wy, vertical, clear, clear);
            Recolor(into.Slice(piece, piece), mask, from, to, vertical, fromGlow, toGlow, outer: false);
            return 2 * piece;
        }

        Cell(
            into[..6], mask, vertical,
            (from.X - wx, from.Y - wy), (to.X - wx, to.Y - wy), to, from, clear, clear, toGlow, fromGlow);
        Cell(
            into.Slice(6, 6), mask, vertical,
            fromOffset, toOffset, (toOffset.Item1 + wx, toOffset.Item2 + wy), (fromOffset.Item1 + wx, fromOffset.Item2 + wy),
            fromGlow, toGlow, clear, clear);
        return 12;
    }

    private static void Recolor(
        Span<MeshVertex> piece, in GridRevealMask mask, (float X, float Y) from, (float X, float Y) to, bool vertical,
        in RenderColor fromGlow, in RenderColor toGlow, bool outer)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var length = (dx * dx) + (dy * dy);
        for (var i = 0; i < piece.Length; i++)
        {
            var vertex = piece[i];
            var inner = outer ? i % 6 is 2 or 4 or 5 : i % 6 is 0 or 1 or 3;
            if (!inner)
            {
                piece[i] = vertex with { Color = RenderColor.Transparent };
                continue;
            }

            var t = length <= 0f ? 0f : Math.Clamp((((vertex.X - from.X) * dx) + ((vertex.Y - from.Y) * dy)) / length, 0f, 1f);
            var glow = GridTone.Mix(fromGlow, toGlow, t);
            var alpha = mask.AlphaAt(vertex.X, vertex.Y, vertical);
            piece[i] = vertex with { Color = new RenderColor(glow.R * alpha, glow.G * alpha, glow.B * alpha, glow.A * alpha) };
        }
    }

    private static void Cell(
        Span<MeshVertex> into, in GridRevealMask mask, bool vertical,
        (float X, float Y) topLeft, (float X, float Y) topRight, (float X, float Y) bottomRight, (float X, float Y) bottomLeft,
        in RenderColor topLeftColor, in RenderColor topRightColor, in RenderColor bottomRightColor, in RenderColor bottomLeftColor)
    {
        if (mask.Clips)
        {
            mask.WriteCell(into, topLeft, topRight, bottomRight, bottomLeft, vertical, topLeftColor, topRightColor, bottomRightColor, bottomLeftColor);
            return;
        }

        into[0] = new MeshVertex(topLeft.X, topLeft.Y, 0, 0, topLeftColor);
        into[1] = new MeshVertex(topRight.X, topRight.Y, 0, 0, topRightColor);
        into[2] = new MeshVertex(bottomLeft.X, bottomLeft.Y, 0, 0, bottomLeftColor);
        into[3] = into[1];
        into[4] = new MeshVertex(bottomRight.X, bottomRight.Y, 0, 0, bottomRightColor);
        into[5] = into[2];
    }
}
