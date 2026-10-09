using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace LunaWm;

public sealed class WifiGlyph : Control
{
    public static readonly StyledProperty<int> BarsProperty =
        AvaloniaProperty.Register<WifiGlyph, int>(nameof(Bars), 3);

    public static readonly StyledProperty<bool> ConnectingProperty =
        AvaloniaProperty.Register<WifiGlyph, bool>(nameof(Connecting));

    private static readonly IBrush On = new SolidColorBrush(Color.FromRgb(0xf3, 0xf3, 0xf3));
    private static readonly IBrush Off = new SolidColorBrush(Color.FromArgb(0x60, 0xf3, 0xf3, 0xf3));

    static WifiGlyph() => AffectsRender<WifiGlyph>(BarsProperty, ConnectingProperty);

    public WifiGlyph()
    {
        Width = 20;
        Height = 18;
    }

    public int Bars
    {
        get => GetValue(BarsProperty);
        set => SetValue(BarsProperty, value);
    }

    public bool Connecting
    {
        get => GetValue(ConnectingProperty);
        set => SetValue(ConnectingProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var center = new Point(10, 15);
        var bars = Math.Clamp(Bars, 0, 3);
        context.DrawEllipse(Connecting || bars > 0 ? On : Off, null, center, 1.8, 1.8);
        for (var i = 1; i <= 3; i++)
        {
            var radius = 3.5 + (i * 3.3);
            var pen = new Pen(Connecting ? (i == 3 ? On : Off) : i <= bars ? On : Off, 2.2, lineCap: PenLineCap.Flat);
            context.DrawGeometry(null, pen, Arc(center, radius));
        }
    }

    private static StreamGeometry Arc(Point center, double radius)
    {
        var geometry = new StreamGeometry();
        using var g = geometry.Open();
        var start = Math.PI * 1.25;
        var end = Math.PI * 1.75;
        g.BeginFigure(new Point(center.X + (radius * Math.Cos(start)), center.Y + (radius * Math.Sin(start))), false);
        g.ArcTo(new Point(center.X + (radius * Math.Cos(end)), center.Y + (radius * Math.Sin(end))), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
        g.EndFigure(false);
        return geometry;
    }
}
