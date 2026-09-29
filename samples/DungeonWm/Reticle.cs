using System.Numerics;
using Basin;

namespace DungeonWm;

internal static class Reticle
{
    public const int Idle = 0;
    public const int Use = 1;
    public const int Grab = 2;
    public const int Dragging = 3;
    public const int ResizeN = 4;
    public const int ResizeNE = 5;
    public const int ResizeE = 6;
    public const int ResizeSE = 7;
    public const int ResizeS = 8;
    public const int ResizeSW = 9;
    public const int ResizeW = 10;
    public const int ResizeNW = 11;

    private const float DimAlpha = 0.35f;
    private const float OutlineAlpha = 0.7f;

    public static readonly string[] Shapes =
        ["idle", "use", "grab", "dragging", "resize-n", "resize-ne", "resize-e", "resize-se", "resize-s", "resize-sw", "resize-w", "resize-nw"];

    private static readonly string[] Names = [.. Shapes.Select(shape => $"reticle-{shape}")];

    private static readonly string[] DimNames = [.. Shapes.Select(shape => $"reticle-{shape}-dim")];

    public static string Name(int shape, bool dim) => dim ? DimNames[shape] : Names[shape];

    public static int Resize(int x, int y) => (x, y) switch
    {
        (0, -1) => ResizeN,
        (1, -1) => ResizeNE,
        (1, 0) => ResizeE,
        (1, 1) => ResizeSE,
        (0, 1) => ResizeS,
        (-1, 1) => ResizeSW,
        (-1, 0) => ResizeW,
        _ => ResizeNW,
    };

    public static XcursorImage Paint(int shape, bool dim, double scale, int size, Vector4 color)
    {
        var pixels = Math.Max(8, (int)Math.Round(size * scale));
        var bytes = new byte[pixels * pixels * 4];
        var step = 2f / pixels;
        var outline = 3f / size;
        var fade = dim ? DimAlpha : 1f;
        for (var y = 0; y < pixels; y++)
        {
            for (var x = 0; x < pixels; x++)
            {
                var p = new Vector2(((x + 0.5f) * step) - 1, ((y + 0.5f) * step) - 1);
                var distance = Distance(shape, p);
                var fill = Math.Clamp(0.5f - (distance / step), 0, 1) * color.W * fade;
                var edge = Math.Clamp(0.5f - ((distance - outline) / step), 0, 1) * OutlineAlpha * fade;
                var alpha = fill + (edge * (1 - fill));
                var at = ((y * pixels) + x) * 4;
                bytes[at] = (byte)MathF.Round(Math.Clamp(color.Z * fill, 0, 1) * 255);
                bytes[at + 1] = (byte)MathF.Round(Math.Clamp(color.Y * fill, 0, 1) * 255);
                bytes[at + 2] = (byte)MathF.Round(Math.Clamp(color.X * fill, 0, 1) * 255);
                bytes[at + 3] = (byte)MathF.Round(Math.Clamp(alpha, 0, 1) * 255);
            }
        }

        return new XcursorImage
        {
            Width = pixels,
            Height = pixels,
            HotspotX = pixels / 2,
            HotspotY = pixels / 2,
            DelayMs = 0,
            Pixels = bytes,
        };
    }

    private static float Distance(int shape, Vector2 p) => shape switch
    {
        Idle => Circle(p, Vector2.Zero, 0.13f),
        Use => MathF.Min(MathF.Abs(Circle(p, Vector2.Zero, 0.44f)) - 0.07f, Circle(p, Vector2.Zero, 0.11f)),
        Grab => OpenHand(p),
        Dragging => ClosedHand(p),
        _ => Arrow(p, Direction(shape)),
    };

    private static Vector2 Direction(int shape)
    {
        var (x, y) = shape switch
        {
            ResizeN => (0, -1),
            ResizeNE => (1, -1),
            ResizeE => (1, 0),
            ResizeSE => (1, 1),
            ResizeS => (0, 1),
            ResizeSW => (-1, 1),
            ResizeW => (-1, 0),
            _ => (-1, -1),
        };
        return Vector2.Normalize(new Vector2(x, y));
    }

    private static float OpenHand(Vector2 p)
    {
        var d = RoundBox(p, new Vector2(0.04f, 0.3f), new Vector2(0.36f, 0.36f), 0.16f);
        d = MathF.Min(d, Capsule(p, new Vector2(-0.24f, 0.05f), new Vector2(-0.27f, -0.52f), 0.09f));
        d = MathF.Min(d, Capsule(p, new Vector2(-0.07f, 0), new Vector2(-0.08f, -0.74f), 0.09f));
        d = MathF.Min(d, Capsule(p, new Vector2(0.1f, 0), new Vector2(0.11f, -0.7f), 0.09f));
        d = MathF.Min(d, Capsule(p, new Vector2(0.27f, 0.05f), new Vector2(0.31f, -0.48f), 0.09f));
        return MathF.Min(d, Capsule(p, new Vector2(-0.28f, 0.36f), new Vector2(-0.66f, -0.02f), 0.1f));
    }

    private static float ClosedHand(Vector2 p)
    {
        var d = RoundBox(p, new Vector2(0.02f, 0.2f), new Vector2(0.4f, 0.36f), 0.18f);
        d = MathF.Min(d, Circle(p, new Vector2(-0.26f, -0.16f), 0.12f));
        d = MathF.Min(d, Circle(p, new Vector2(-0.08f, -0.21f), 0.12f));
        d = MathF.Min(d, Circle(p, new Vector2(0.1f, -0.21f), 0.12f));
        d = MathF.Min(d, Circle(p, new Vector2(0.28f, -0.16f), 0.12f));
        return MathF.Min(d, Capsule(p, new Vector2(-0.4f, 0.3f), new Vector2(-0.52f, 0.02f), 0.1f));
    }

    private static float Arrow(Vector2 p, Vector2 direction)
    {
        var across = new Vector2(-direction.Y, direction.X);
        var d = Capsule(p, direction * -0.52f, direction * 0.2f, 0.08f);
        d = MathF.Min(d, Triangle(p, direction * 0.7f, (direction * 0.2f) + (across * 0.34f), (direction * 0.2f) - (across * 0.34f)));
        return MathF.Min(d, Capsule(p, (direction * 0.82f) + (across * 0.36f), (direction * 0.82f) - (across * 0.36f), 0.06f));
    }

    private static float Circle(Vector2 p, Vector2 center, float radius) => Vector2.Distance(p, center) - radius;

    private static float Capsule(Vector2 p, Vector2 a, Vector2 b, float radius)
    {
        var pa = p - a;
        var ba = b - a;
        var h = Math.Clamp(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba), 0, 1);
        return (pa - (ba * h)).Length() - radius;
    }

    private static float RoundBox(Vector2 p, Vector2 center, Vector2 half, float radius)
    {
        var q = Vector2.Abs(p - center) - half + new Vector2(radius);
        return Vector2.Max(q, Vector2.Zero).Length() + MathF.Min(MathF.Max(q.X, q.Y), 0) - radius;
    }

    private static float Triangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        var e0 = b - a;
        var e1 = c - b;
        var e2 = a - c;
        var v0 = p - a;
        var v1 = p - b;
        var v2 = p - c;
        var pq0 = v0 - (e0 * Math.Clamp(Vector2.Dot(v0, e0) / Vector2.Dot(e0, e0), 0, 1));
        var pq1 = v1 - (e1 * Math.Clamp(Vector2.Dot(v1, e1) / Vector2.Dot(e1, e1), 0, 1));
        var pq2 = v2 - (e2 * Math.Clamp(Vector2.Dot(v2, e2) / Vector2.Dot(e2, e2), 0, 1));
        var s = MathF.Sign((e0.X * e2.Y) - (e0.Y * e2.X));
        var d0 = new Vector2(pq0.LengthSquared(), s * ((v0.X * e0.Y) - (v0.Y * e0.X)));
        var d1 = new Vector2(pq1.LengthSquared(), s * ((v1.X * e1.Y) - (v1.Y * e1.X)));
        var d2 = new Vector2(pq2.LengthSquared(), s * ((v2.X * e2.Y) - (v2.Y * e2.X)));
        var d = Vector2.Min(Vector2.Min(d0, d1), d2);
        return -MathF.Sqrt(d.X) * MathF.Sign(d.Y);
    }
}
