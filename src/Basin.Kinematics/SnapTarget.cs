namespace Basin.Kinematics;

public readonly record struct SnapTarget(SnapKind Kind, double X, double Y, double StepX, double StepY)
{
    public static SnapTarget Point(double x, double y) => new(SnapKind.Point, x, y, 0, 0);

    public static SnapTarget VerticalLine(double x) => new(SnapKind.VerticalLine, x, 0, 0, 0);

    public static SnapTarget HorizontalLine(double y) => new(SnapKind.HorizontalLine, 0, y, 0, 0);

    public static SnapTarget Grid(double originX, double originY, double stepX, double stepY) =>
        new(SnapKind.Grid, originX, originY, stepX, stepY);

    public (double X, double Y) Nearest(double x, double y) => Kind switch
    {
        SnapKind.Point => (X, Y),
        SnapKind.VerticalLine => (X, y),
        SnapKind.HorizontalLine => (x, Y),
        _ => (Lattice(x, X, StepX), Lattice(y, Y, StepY)),
    };

    private static double Lattice(double value, double origin, double step) =>
        step > 0 ? origin + (Math.Round((value - origin) / step, MidpointRounding.AwayFromZero) * step) : value;
}
