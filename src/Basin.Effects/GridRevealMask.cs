using Basin.Scene;

namespace Basin.Effects;

internal struct GridRevealMask
{
    public const int LineVertices = 18;

    private const double Stagger = 0.4;

    private const double Far = 1e9;

    private GridRevealStyle _style;
    private double _progress;
    private double _originX;
    private double _originY;
    private double _feather;
    private double _radius;
    private double _front;
    private int _edge;
    private Box _bounds;
    private double _reachAlongY;
    private double _reachAlongX;
    private double _spreadX;
    private double _spreadY;

    public bool Clips { get; private set; }

    public float Scale { get; private set; }

    public static bool ClipsFor(in GridReveal reveal) =>
        !(reveal.Progress >= 1f) && reveal.Style is GridRevealStyle.Ripple or GridRevealStyle.Draw or GridRevealStyle.Sweep;

    public static GridRevealMask Of(in GridReveal reveal, in Box bounds)
    {
        var mask = new GridRevealMask { Scale = 1f };
        if (reveal.Progress >= 1f)
        {
            return mask;
        }

        var progress = float.IsNaN(reveal.Progress) ? 0f : Math.Max(0f, reveal.Progress);
        if (!ClipsFor(reveal))
        {
            mask.Scale = reveal.Style == GridRevealStyle.Fade ? progress : 0f;
            return mask;
        }

        mask.Clips = true;
        mask._style = reveal.Style;
        mask._progress = progress;
        mask._originX = double.IsFinite(reveal.OriginX) ? reveal.OriginX : bounds.X + (bounds.Width / 2.0);
        mask._originY = double.IsFinite(reveal.OriginY) ? reveal.OriginY : bounds.Y + (bounds.Height / 2.0);
        mask._feather = double.IsFinite(reveal.Feather) ? Math.Max(1e-3, reveal.Feather) : 1e-3;
        mask._bounds = bounds;
        var left = mask._originX - bounds.X;
        var right = bounds.Right - mask._originX;
        var top = mask._originY - bounds.Y;
        var bottom = bounds.Bottom - mask._originY;
        switch (reveal.Style)
        {
            case GridRevealStyle.Ripple:
                var far = Math.Sqrt((Math.Max(left, right) * Math.Max(left, right)) + (Math.Max(top, bottom) * Math.Max(top, bottom)));
                mask._radius = progress * (far + mask._feather);
                break;
            case GridRevealStyle.Draw:
                mask._reachAlongY = Math.Max(1.0, Math.Max(top, bottom));
                mask._reachAlongX = Math.Max(1.0, Math.Max(left, right));
                mask._spreadX = Math.Max(1.0, Math.Max(left, right));
                mask._spreadY = Math.Max(1.0, Math.Max(top, bottom));
                break;
            default:
                var nearest = Math.Min(Math.Min(left, right), Math.Min(top, bottom));
                mask._edge = nearest == left ? 0 : nearest == right ? 1 : nearest == top ? 2 : 3;
                var extent = mask._edge < 2 ? bounds.Width : bounds.Height;
                mask._front = progress * (extent + mask._feather);
                break;
        }

        return mask;
    }

    public readonly float AlphaAt(double x, double y, bool vertical) => AlphaAt(x, y, vertical, double.NaN);

    private readonly float AlphaAt(double x, double y, bool vertical, double across)
    {
        if (_progress <= 0.0)
        {
            return 0f;
        }

        switch (_style)
        {
            case GridRevealStyle.Ripple:
                var dx = x - _originX;
                var dy = y - _originY;
                return Smooth((_radius - Math.Sqrt((dx * dx) + (dy * dy))) / _feather);
            case GridRevealStyle.Draw:
                if (double.IsNaN(across))
                {
                    across = vertical ? Math.Abs(x - _originX) : Math.Abs(y - _originY);
                }

                var reach = DrawReach(across, vertical ? _spreadX : _spreadY, vertical ? _reachAlongY : _reachAlongX);
                return Smooth((reach - (vertical ? Math.Abs(y - _originY) : Math.Abs(x - _originX))) / _feather);
            default:
                return Smooth((_front - SweepDepth(x, y)) / _feather);
        }
    }

    public readonly void WriteCell(
        Span<MeshVertex> into,
        (float X, float Y) topLeft,
        (float X, float Y) topRight,
        (float X, float Y) bottomRight,
        (float X, float Y) bottomLeft,
        bool vertical,
        in RenderColor color) =>
        WriteCell(into, topLeft, topRight, bottomRight, bottomLeft, vertical, double.NaN, color);

    private readonly void WriteCell(
        Span<MeshVertex> into,
        (float X, float Y) topLeft,
        (float X, float Y) topRight,
        (float X, float Y) bottomRight,
        (float X, float Y) bottomLeft,
        bool vertical,
        double across,
        in RenderColor color)
    {
        into[0] = Vertex(topLeft, vertical, across, color);
        into[1] = Vertex(topRight, vertical, across, color);
        into[2] = Vertex(bottomLeft, vertical, across, color);
        into[3] = into[1];
        into[4] = Vertex(bottomRight, vertical, across, color);
        into[5] = into[2];
    }

    public readonly void WriteLine(
        Span<MeshVertex> into, (float X, float Y) start, (float X, float Y) end, float nx, float ny, bool vertical, in RenderColor color)
    {
        if (!Interval(start, end, vertical, out var outer0, out var core0, out var core1, out var outer1))
        {
            var hidden = new MeshVertex(start.X, start.Y, 0, 0, RenderColor.Transparent);
            into[..LineVertices].Fill(hidden);
            return;
        }

        var across = Across(start, end, vertical);
        Piece(into[..6], start, end, outer0, core0, nx, ny, vertical, across, color);
        Piece(into.Slice(6, 6), start, end, core0, core1, nx, ny, vertical, across, color);
        Piece(into.Slice(12, 6), start, end, core1, outer1, nx, ny, vertical, across, color);
    }

    public readonly void WriteLine(
        Span<MeshVertex> into, (float X, float Y) start, (float X, float Y) end, float nx, float ny, bool vertical,
        in RenderColor startColor, in RenderColor endColor)
    {
        if (!Interval(start, end, vertical, out var outer0, out var core0, out var core1, out var outer1))
        {
            var hidden = new MeshVertex(start.X, start.Y, 0, 0, RenderColor.Transparent);
            into[..LineVertices].Fill(hidden);
            return;
        }

        var across = Across(start, end, vertical);
        Piece(into[..6], start, end, outer0, core0, nx, ny, vertical, across, startColor, endColor);
        Piece(into.Slice(6, 6), start, end, core0, core1, nx, ny, vertical, across, startColor, endColor);
        Piece(into.Slice(12, 6), start, end, core1, outer1, nx, ny, vertical, across, startColor, endColor);
    }

    public readonly void WriteCell(
        Span<MeshVertex> into,
        (float X, float Y) topLeft,
        (float X, float Y) topRight,
        (float X, float Y) bottomRight,
        (float X, float Y) bottomLeft,
        bool vertical,
        in RenderColor topLeftColor,
        in RenderColor topRightColor,
        in RenderColor bottomRightColor,
        in RenderColor bottomLeftColor)
    {
        into[0] = Vertex(topLeft, vertical, double.NaN, topLeftColor);
        into[1] = Vertex(topRight, vertical, double.NaN, topRightColor);
        into[2] = Vertex(bottomLeft, vertical, double.NaN, bottomLeftColor);
        into[3] = into[1];
        into[4] = Vertex(bottomRight, vertical, double.NaN, bottomRightColor);
        into[5] = into[2];
    }

    private readonly void Piece(
        Span<MeshVertex> into, (float X, float Y) start, (float X, float Y) end, double from, double to,
        float nx, float ny, bool vertical, double across, in RenderColor startColor, in RenderColor endColor)
    {
        var a = At(start, end, from);
        var b = At(start, end, to);
        var ca = GridTone.Mix(startColor, endColor, (float)from);
        var cb = GridTone.Mix(startColor, endColor, (float)to);
        into[0] = Vertex(a, vertical, across, ca);
        into[1] = Vertex(b, vertical, across, cb);
        into[2] = Vertex((a.X + nx, a.Y + ny), vertical, across, ca);
        into[3] = into[1];
        into[4] = Vertex((b.X + nx, b.Y + ny), vertical, across, cb);
        into[5] = into[2];
    }

    private readonly void Piece(
        Span<MeshVertex> into, (float X, float Y) start, (float X, float Y) end, double from, double to,
        float nx, float ny, bool vertical, double across, in RenderColor color)
    {
        var a = At(start, end, from);
        var b = At(start, end, to);
        WriteCell(into, a, b, (b.X + nx, b.Y + ny), (a.X + nx, a.Y + ny), vertical, across, color);
    }

    private readonly double Across((float X, float Y) start, (float X, float Y) end, bool vertical) =>
        _style != GridRevealStyle.Draw ? double.NaN
        : vertical ? Math.Abs(((start.X + end.X) / 2.0) - _originX)
        : Math.Abs(((start.Y + end.Y) / 2.0) - _originY);

    private static (float X, float Y) At((float X, float Y) start, (float X, float Y) end, double t) =>
        ((float)(start.X + ((end.X - start.X) * t)), (float)(start.Y + ((end.Y - start.Y) * t)));

    private readonly bool Interval(
        (float X, float Y) start, (float X, float Y) end, bool vertical,
        out double outer0, out double core0, out double core1, out double outer1)
    {
        if (_progress <= 0.0)
        {
            outer0 = core0 = core1 = outer1 = 0.0;
            return false;
        }

        switch (_style)
        {
            case GridRevealStyle.Ripple:
                return Chord(start, end, out outer0, out core0, out core1, out outer1);
            case GridRevealStyle.Draw:
                var reach = DrawReach(
                    Across(start, end, vertical), vertical ? _spreadX : _spreadY, vertical ? _reachAlongY : _reachAlongX);
                var center = vertical ? _originY : _originX;
                return Band(
                    vertical ? start.Y : start.X, vertical ? end.Y - start.Y : end.X - start.X,
                    center - reach, center + reach, center - reach + _feather, center + reach - _feather,
                    out outer0, out core0, out core1, out outer1);
            default:
                var horizontal = _edge < 2;
                var from = horizontal ? start.X : start.Y;
                var delta = horizontal ? end.X - start.X : end.Y - start.Y;
                return _edge switch
                {
                    0 => Band(from, delta, -Far, _bounds.X + _front, -Far, _bounds.X + _front - _feather,
                        out outer0, out core0, out core1, out outer1),
                    1 => Band(from, delta, _bounds.Right - _front, Far, _bounds.Right - _front + _feather, Far,
                        out outer0, out core0, out core1, out outer1),
                    2 => Band(from, delta, -Far, _bounds.Y + _front, -Far, _bounds.Y + _front - _feather,
                        out outer0, out core0, out core1, out outer1),
                    _ => Band(from, delta, _bounds.Bottom - _front, Far, _bounds.Bottom - _front + _feather, Far,
                        out outer0, out core0, out core1, out outer1),
                };
        }
    }

    private readonly bool Chord(
        (float X, float Y) start, (float X, float Y) end,
        out double outer0, out double core0, out double core1, out double outer1)
    {
        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        var sx = start.X - _originX;
        var sy = start.Y - _originY;
        var a = (dx * dx) + (dy * dy);
        var b = (sx * dx) + (sy * dy);
        var c = (sx * sx) + (sy * sy);
        core0 = core1 = 0.0;
        if (a < 1e-12)
        {
            outer0 = 0.0;
            outer1 = 1.0;
            return c < _radius * _radius;
        }

        if (!Roots(a, b, c, _radius, out outer0, out outer1))
        {
            return false;
        }

        var inner = _radius - _feather;
        if (inner <= 0.0 || !Roots(a, b, c, inner, out core0, out core1) ||
            Math.Max(outer0, core0) >= Math.Min(outer1, core1))
        {
            core0 = core1 = Math.Clamp(-b / a, outer0, outer1);
            return true;
        }

        core0 = Math.Max(outer0, core0);
        core1 = Math.Min(outer1, core1);
        return true;
    }

    private static bool Roots(double a, double b, double c, double radius, out double t0, out double t1)
    {
        var discriminant = (b * b) - (a * (c - (radius * radius)));
        if (discriminant <= 0.0)
        {
            t0 = t1 = 0.0;
            return false;
        }

        var root = Math.Sqrt(discriminant);
        t0 = Math.Max(0.0, (-b - root) / a);
        t1 = Math.Min(1.0, (-b + root) / a);
        return t0 < t1;
    }

    private static bool Band(
        double from, double delta, double low, double high, double coreLow, double coreHigh,
        out double outer0, out double core0, out double core1, out double outer1)
    {
        if (Math.Abs(delta) < 1e-9)
        {
            outer0 = 0.0;
            outer1 = 1.0;
            var inside = from > low && from < high;
            var whole = from >= coreLow && from <= coreHigh;
            core0 = whole ? 0.0 : 0.5;
            core1 = whole ? 1.0 : 0.5;
            return inside;
        }

        Span(from, delta, low, high, out outer0, out outer1);
        if (outer0 >= outer1)
        {
            core0 = core1 = 0.0;
            return false;
        }

        Span(from, delta, coreLow, coreHigh, out core0, out core1);
        core0 = Math.Max(outer0, core0);
        core1 = Math.Min(outer1, core1);
        if (coreLow > coreHigh || core0 > core1)
        {
            core0 = core1 = Math.Clamp((((low + high) / 2.0) - from) / delta, outer0, outer1);
        }

        return true;
    }

    private static void Span(double from, double delta, double low, double high, out double t0, out double t1)
    {
        var a = (low - from) / delta;
        var b = (high - from) / delta;
        t0 = Math.Max(0.0, Math.Min(a, b));
        t1 = Math.Min(1.0, Math.Max(a, b));
    }

    private readonly double DrawReach(double across, double spread, double along)
    {
        var start = Stagger * Math.Min(1.0, across / spread);
        var local = Math.Clamp((_progress - start) / (1.0 - start), 0.0, 1.0);
        return local * (along + _feather);
    }

    private readonly double SweepDepth(double x, double y) => _edge switch
    {
        0 => x - _bounds.X,
        1 => _bounds.Right - x,
        2 => y - _bounds.Y,
        _ => _bounds.Bottom - y,
    };

    private readonly MeshVertex Vertex((float X, float Y) point, bool vertical, double across, in RenderColor color)
    {
        var alpha = AlphaAt(point.X, point.Y, vertical, across);
        return new MeshVertex(
            point.X, point.Y, 0, 0, new RenderColor(color.R * alpha, color.G * alpha, color.B * alpha, color.A * alpha));
    }

    private static float Smooth(double value)
    {
        if (value <= 1e-4)
        {
            return 0f;
        }

        var t = Math.Min(value, 1.0);
        return (float)(t * t * (3.0 - (2.0 * t)));
    }
}
