using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace LunaWm;

public sealed class BatteryGlyph : Control
{
    public static readonly StyledProperty<int> LevelProperty =
        AvaloniaProperty.Register<BatteryGlyph, int>(nameof(Level), 11);

    public static readonly StyledProperty<bool> ChargingProperty =
        AvaloniaProperty.Register<BatteryGlyph, bool>(nameof(Charging));

    public static readonly StyledProperty<bool> ErrorProperty =
        AvaloniaProperty.Register<BatteryGlyph, bool>(nameof(Error));

    private static readonly IBrush Full = new SolidColorBrush(Color.FromRgb(0xf3, 0xf3, 0xf3));
    private static readonly IBrush Empty = new SolidColorBrush(Color.FromRgb(0x4c, 0x4c, 0x4c));
    private static readonly IBrush Bolt = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
    private static readonly Pen BoltEdge = new(new SolidColorBrush(Color.FromRgb(0x27, 0x27, 0x27)), 1);

    static BatteryGlyph() => AffectsRender<BatteryGlyph>(LevelProperty, ChargingProperty, ErrorProperty);

    public BatteryGlyph()
    {
        Width = 17;
        Height = 20;
    }

    public int Level
    {
        get => GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    public bool Charging
    {
        get => GetValue(ChargingProperty);
        set => SetValue(ChargingProperty, value);
    }

    public bool Error
    {
        get => GetValue(ErrorProperty);
        set => SetValue(ErrorProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var level = Math.Clamp(Level, 0, 11);
        context.FillRectangle(level >= 11 ? Full : Empty, new Rect(7, 0, 3, 2));
        var body = new Rect(3, 2, 11, 16);
        context.FillRectangle(Empty, body);
        var filled = Math.Round(16 * level / 11.0);
        context.FillRectangle(Error ? Empty : Full, new Rect(3, 18 - filled, 11, filled));
        if (Error)
        {
            context.DrawLine(new Pen(Full, 2), new Point(5, 5), new Point(12, 15));
            context.DrawLine(new Pen(Full, 2), new Point(12, 5), new Point(5, 15));
        }

        if (Charging)
        {
            var bolt = new StreamGeometry();
            using (var g = bolt.Open())
            {
                g.BeginFigure(new Point(16.5, 4), true);
                g.LineTo(new Point(11, 11));
                g.LineTo(new Point(14, 11));
                g.LineTo(new Point(12, 17));
                g.LineTo(new Point(17, 9.5));
                g.LineTo(new Point(14.2, 9.5));
                g.EndFigure(true);
            }

            context.DrawGeometry(Bolt, BoltEdge, bolt);
        }
    }
}
