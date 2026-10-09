using Avalonia;
using Avalonia.Media;

namespace LunaWm;

internal static class KeyboardGlyphs
{
    public static readonly Size ShiftSize = new(50, 50);
    public static readonly Size BackspaceSize = new(50, 38);
    public static readonly Size HideSize = new(50, 44);

    public static readonly Geometry Shift = Arrow();

    public static readonly Geometry ShiftLockBar = Rectangle(17, 41, 16, 4);

    public static readonly Geometry Backspace = BackspaceShape();

    public static readonly Geometry BackspaceCross = Cross();

    public static readonly Geometry HideBody = HideKeyboard();

    public static readonly Geometry HideChevron = Chevron();

    private static StreamGeometry Arrow()
    {
        var geometry = new StreamGeometry();
        using var g = geometry.Open();
        g.BeginFigure(new Point(25, 8), true);
        g.LineTo(new Point(42, 26));
        g.LineTo(new Point(32, 26));
        g.LineTo(new Point(32, 37));
        g.LineTo(new Point(18, 37));
        g.LineTo(new Point(18, 26));
        g.LineTo(new Point(8, 26));
        g.EndFigure(true);
        return geometry;
    }

    private static StreamGeometry BackspaceShape()
    {
        var geometry = new StreamGeometry();
        using var g = geometry.Open();
        g.BeginFigure(new Point(4, 19), true);
        g.LineTo(new Point(17, 6));
        g.LineTo(new Point(45, 6));
        g.LineTo(new Point(45, 32));
        g.LineTo(new Point(17, 32));
        g.EndFigure(true);
        return geometry;
    }

    private static StreamGeometry Cross()
    {
        var geometry = new StreamGeometry();
        using var g = geometry.Open();
        g.BeginFigure(new Point(23, 12), false);
        g.LineTo(new Point(37, 26));
        g.EndFigure(false);
        g.BeginFigure(new Point(37, 12), false);
        g.LineTo(new Point(23, 26));
        g.EndFigure(false);
        return geometry;
    }

    private static StreamGeometry HideKeyboard()
    {
        var geometry = new StreamGeometry();
        using var g = geometry.Open();
        g.BeginFigure(new Point(6, 4), true);
        g.LineTo(new Point(44, 4));
        g.LineTo(new Point(44, 28));
        g.LineTo(new Point(6, 28));
        g.EndFigure(true);
        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 6; column++)
            {
                if (row == 2 && column is > 0 and < 5)
                {
                    continue;
                }

                var x = 10 + (column * 5.5);
                var y = 8 + (row * 6);
                g.BeginFigure(new Point(x, y), true);
                g.LineTo(new Point(x + 3.5, y));
                g.LineTo(new Point(x + 3.5, y + 3.5));
                g.LineTo(new Point(x, y + 3.5));
                g.EndFigure(true);
            }
        }

        g.BeginFigure(new Point(16, 20), true);
        g.LineTo(new Point(34, 20));
        g.LineTo(new Point(34, 23.5));
        g.LineTo(new Point(16, 23.5));
        g.EndFigure(true);
        return geometry;
    }

    private static StreamGeometry Chevron()
    {
        var geometry = new StreamGeometry();
        using var g = geometry.Open();
        g.BeginFigure(new Point(17, 33), false);
        g.LineTo(new Point(25, 40));
        g.LineTo(new Point(33, 33));
        g.EndFigure(false);
        return geometry;
    }

    private static StreamGeometry Rectangle(double x, double y, double width, double height)
    {
        var geometry = new StreamGeometry();
        using var g = geometry.Open();
        g.BeginFigure(new Point(x, y), true);
        g.LineTo(new Point(x + width, y));
        g.LineTo(new Point(x + width, y + height));
        g.LineTo(new Point(x, y + height));
        g.EndFigure(true);
        return geometry;
    }
}
