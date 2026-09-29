using Basin.Scene;

namespace Basin.Effects;

public sealed class CanvasStepMap
{
    public double CenterX { get; private set; }

    public double CenterY { get; private set; }

    public double Zoom { get; private set; } = 1.0;

    public double ShelfScale { get; private set; } = 1.0;

    public FBox Outline { get; private set; }

    public FBox Inner { get; private set; }

    public FBox Outer { get; private set; }

    public CanvasStepSides Sides { get; private set; }

    public CanvasStepSides Shelves { get; private set; }

    public bool IsIdentity => Zoom == 1.0 && ShelfScale == 1.0;

    public const double MaxBulge = 0.7;

    private const int BendIterations = 8;

    private readonly double[] _bulgeCenter = new double[4];
    private readonly double[] _bulgeDepth = new double[4];

    public bool Bulges { get; set; }

    public bool SetBulge(CanvasStepSides side, double center, double depth)
    {
        var index = BulgeIndex(side);
        depth = double.IsFinite(depth) ? Math.Clamp(depth, 0.0, MaxBulge) : 0.0;
        if (index < 0 || (_bulgeCenter[index] == center && _bulgeDepth[index] == depth))
        {
            return false;
        }

        _bulgeCenter[index] = center;
        _bulgeDepth[index] = depth;
        return true;
    }

    public double BulgeCenter(CanvasStepSides side) => BulgeIndex(side) is >= 0 and var index ? _bulgeCenter[index] : 0.0;

    public double BulgeDepth(CanvasStepSides side) => BulgeIndex(side) is >= 0 and var index ? _bulgeDepth[index] : 0.0;

    public double BulgeAt(CanvasStepSides side, double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        return BulgeDepth(side) * 4.0 * t * (1.0 - t);
    }

    public (double Start, double Length) WallSpan(CanvasStepSides side, double t)
    {
        var vertical = side is CanvasStepSides.Left or CanvasStepSides.Right;
        var innerStart = vertical ? Outline.Y : Outline.X;
        var innerLength = vertical ? Outline.Height : Outline.Width;
        var outerStart = vertical ? Inner.Y : Inner.X;
        var outerLength = vertical ? Inner.Height : Inner.Width;
        return (innerStart + ((outerStart - innerStart) * t), innerLength + ((outerLength - innerLength) * t));
    }

    public (double X, double Y) Bend(CanvasStepSides side, double x, double y, double t)
    {
        var k = BulgeAt(side, t);
        if (k == 0)
        {
            return (x, y);
        }

        var (start, length) = WallSpan(side, t);
        var center = BulgeCenter(side);
        return side is CanvasStepSides.Left or CanvasStepSides.Right
            ? (x, BendAlong(y, k, center, start, length))
            : (BendAlong(x, k, center, start, length), y);
    }

    public (double X, double Y) Unbend(CanvasStepSides side, double x, double y, double t)
    {
        var k = BulgeAt(side, t);
        if (k == 0)
        {
            return (x, y);
        }

        var (start, length) = WallSpan(side, t);
        var center = BulgeCenter(side);
        return side is CanvasStepSides.Left or CanvasStepSides.Right
            ? (x, UnbendAlong(y, k, center, start, length))
            : (UnbendAlong(x, k, center, start, length), y);
    }

    private static double BendAlong(double value, double k, double center, double start, double length)
    {
        if (length <= 0)
        {
            return value;
        }

        var a = Math.Clamp((value - start) / length, 0.0, 1.0);
        return value + (k * 4.0 * a * (1.0 - a) * (center - value));
    }

    private static double UnbendAlong(double bent, double k, double center, double start, double length)
    {
        if (length <= 0)
        {
            return bent;
        }

        var value = bent;
        for (var i = 0; i < BendIterations; i++)
        {
            var a = Math.Clamp((value - start) / length, 0.0, 1.0);
            var envelope = 4.0 * a * (1.0 - a);
            var slope = 4.0 * (1.0 - (2.0 * a)) / length;
            var f = value + (k * envelope * (center - value)) - bent;
            var df = 1.0 + (k * ((slope * (center - value)) - envelope));
            value -= f / df;
        }

        return value;
    }

    private static int BulgeIndex(CanvasStepSides side) => side switch
    {
        CanvasStepSides.Left => 0,
        CanvasStepSides.Right => 1,
        CanvasStepSides.Top => 2,
        CanvasStepSides.Bottom => 3,
        _ => -1,
    };

    public bool Layout(
        double centerX, double centerY, double zoom, double shelfScale, in FBox outline, in FBox inner, in FBox outer, CanvasStepSides sides) =>
        Layout(centerX, centerY, zoom, shelfScale, outline, inner, outer, sides, sides);

    public bool Layout(
        double centerX, double centerY, double zoom, double shelfScale, in FBox outline, in FBox inner, in FBox outer,
        CanvasStepSides sides, CanvasStepSides shelves)
    {
        zoom = zoom > 0 && double.IsFinite(zoom) ? zoom : 1.0;
        shelfScale = shelfScale > 0 && double.IsFinite(shelfScale) ? shelfScale : 1.0;
        shelves &= sides;
        if (CenterX == centerX && CenterY == centerY && Zoom == zoom && ShelfScale == shelfScale &&
            Outline == outline && Inner == inner && Outer == outer && Sides == sides && Shelves == shelves)
        {
            return false;
        }

        Shelves = shelves;

        CenterX = centerX;
        CenterY = centerY;
        Zoom = zoom;
        ShelfScale = shelfScale;
        Outline = outline;
        Inner = inner;
        Outer = outer;
        Sides = sides;
        return true;
    }

    public double ScaleOf(CanvasStepPlane plane) => plane == CanvasStepPlane.Shelf ? ShelfScale : Zoom;

    public double WallWidth(CanvasStepSides side)
    {
        if ((Sides & side) != side || side == CanvasStepSides.None)
        {
            return 0.0;
        }

        return side switch
        {
            CanvasStepSides.Left => Outline.X - Inner.X,
            CanvasStepSides.Right => Inner.Right - Outline.Right,
            CanvasStepSides.Top => Outline.Y - Inner.Y,
            CanvasStepSides.Bottom => Inner.Bottom - Outline.Bottom,
            _ => 0.0,
        };
    }

    public CanvasStepPlane PlaneAt(double canvasX, double canvasY)
    {
        var x = CenterX + ((canvasX - CenterX) * ShelfScale);
        var y = CenterY + ((canvasY - CenterY) * ShelfScale);
        var shelf = ((Shelves & CanvasStepSides.Left) != 0 && x <= Inner.X) ||
            ((Shelves & CanvasStepSides.Right) != 0 && x >= Inner.Right) ||
            ((Shelves & CanvasStepSides.Top) != 0 && y <= Inner.Y) ||
            ((Shelves & CanvasStepSides.Bottom) != 0 && y >= Inner.Bottom);
        return shelf ? CanvasStepPlane.Shelf : CanvasStepPlane.Desktop;
    }

    public CanvasStepPlane PlaneOf(in Box canvasBox) =>
        PlaneAt(canvasBox.X + (canvasBox.Width / 2.0), canvasBox.Y + (canvasBox.Height / 2.0));

    public RenderTransform DesktopPlacement(double preScale, double anchorX, double anchorY) =>
        Placement(CanvasStepPlane.Desktop, preScale, anchorX, anchorY);

    public RenderTransform ShelfPlacement(double preScale, double anchorX, double anchorY) =>
        Placement(CanvasStepPlane.Shelf, preScale, anchorX, anchorY);

    public RenderTransform Placement(CanvasStepPlane plane, double preScale, double anchorX, double anchorY)
    {
        var scale = ScaleOf(plane);
        if (scale == 1.0 && preScale == 1.0)
        {
            return RenderTransform.Identity;
        }

        var total = scale * preScale;
        return new RenderTransform(
            total, 0, (CenterX * (1.0 - scale)) + (scale * anchorX * (1.0 - preScale)),
            0, total, (CenterY * (1.0 - scale)) + (scale * anchorY * (1.0 - preScale)),
            0, 0, 1);
    }

    public (double X, double Y) ToScreen(double canvasX, double canvasY) =>
        ToScreen(PlaneAt(canvasX, canvasY), canvasX, canvasY);

    public (double X, double Y) ToScreen(CanvasStepPlane plane, double canvasX, double canvasY)
    {
        var scale = ScaleOf(plane);
        return (CenterX + ((canvasX - CenterX) * scale), CenterY + ((canvasY - CenterY) * scale));
    }

    public (double X, double Y) ToCanvas(CanvasStepPlane plane, double screenX, double screenY)
    {
        var scale = ScaleOf(plane);
        return (CenterX + ((screenX - CenterX) / scale), CenterY + ((screenY - CenterY) / scale));
    }

    public bool TryToCanvas(double screenX, double screenY, out CanvasStepPlane plane, out double canvasX, out double canvasY)
    {
        plane = CanvasStepPlane.Desktop;
        canvasX = screenX;
        canvasY = screenY;
        if (screenX < Outer.X || screenX >= Outer.Right || screenY < Outer.Y || screenY >= Outer.Bottom)
        {
            return false;
        }

        var depth = WallDepth(screenX, screenY, out var side);
        if (depth > 0.0 && (depth < 1.0 || (side & Shelves) != side))
        {
            return false;
        }

        plane = depth >= 1.0 ? CanvasStepPlane.Shelf : CanvasStepPlane.Desktop;
        (canvasX, canvasY) = ToCanvas(plane, screenX, screenY);
        return true;
    }

    public (double X, double Y) ToCanvasNearest(double screenX, double screenY, out CanvasStepPlane plane)
    {
        plane = WallDepth(screenX, screenY, out _) > 0.5 ? CanvasStepPlane.Shelf : CanvasStepPlane.Desktop;
        return ToCanvas(plane, screenX, screenY);
    }

    public double WallDepth(double screenX, double screenY, out CanvasStepSides side)
    {
        var across = double.NegativeInfinity;
        var along = double.NegativeInfinity;
        var horizontal = CanvasStepSides.None;
        var vertical = CanvasStepSides.None;
        Depth(CanvasStepSides.Left, -(screenX - Outline.X), ref across, ref horizontal);
        Depth(CanvasStepSides.Right, screenX - Outline.Right, ref across, ref horizontal);
        Depth(CanvasStepSides.Top, -(screenY - Outline.Y), ref along, ref vertical);
        Depth(CanvasStepSides.Bottom, screenY - Outline.Bottom, ref along, ref vertical);
        if (horizontal == CanvasStepSides.None && vertical == CanvasStepSides.None)
        {
            side = CanvasStepSides.None;
            return double.NegativeInfinity;
        }

        if (across >= 1.0 && along >= 1.0)
        {
            side = horizontal | vertical;
            return Math.Max(across, along);
        }

        side = across >= along ? horizontal : vertical;
        return Math.Max(across, along);
    }

    private void Depth(CanvasStepSides side, double past, ref double deepest, ref CanvasStepSides owner)
    {
        var width = WallWidth(side);
        if (width <= 0.0)
        {
            return;
        }

        var depth = past / width;
        if (depth > deepest)
        {
            deepest = depth;
            owner = side;
        }
    }

    public double DragScale(double depth)
    {
        var t = Math.Clamp(double.IsNaN(depth) ? 0.0 : depth, 0.0, 1.0);
        var eased = t * t * (3.0 - (2.0 * t));
        return Zoom + ((ShelfScale - Zoom) * eased);
    }

    public FBox ShelfStrip(CanvasStepSides side)
    {
        if (side == CanvasStepSides.None || (Shelves & side) != side)
        {
            return default;
        }

        var left = Outer.X;
        var top = Outer.Y;
        var right = Outer.Right;
        var bottom = Outer.Bottom;
        if ((side & CanvasStepSides.Left) != 0)
        {
            right = Math.Min(right, Inner.X);
        }

        if ((side & CanvasStepSides.Right) != 0)
        {
            left = Math.Max(left, Inner.Right);
        }

        if ((side & CanvasStepSides.Top) != 0)
        {
            bottom = Math.Min(bottom, Inner.Y);
        }

        if ((side & CanvasStepSides.Bottom) != 0)
        {
            top = Math.Max(top, Inner.Bottom);
        }

        return right <= left || bottom <= top ? default : new FBox(left, top, right - left, bottom - top);
    }

    public double ShelfFit(double width, double height, CanvasStepSides side, double minScale)
    {
        var strip = ShelfStrip(side);
        if (strip.IsEmpty || width <= 0 || height <= 0)
        {
            return 1.0;
        }

        var floor = minScale / ShelfScale;
        var fitX = Math.Min(1.0, Math.Max(floor, strip.Width / (width * ShelfScale)));
        var fitY = Math.Min(1.0, Math.Max(floor, strip.Height / (height * ShelfScale)));
        return Math.Min(fitX, fitY);
    }
}
