using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace LunaWm;

public sealed class VolumeGlyph : Control
{
    public static readonly StyledProperty<bool> MutedProperty =
        AvaloniaProperty.Register<VolumeGlyph, bool>(nameof(Muted));

    public static readonly StyledProperty<double> LevelProperty =
        AvaloniaProperty.Register<VolumeGlyph, double>(nameof(Level), 1);

    private static readonly IBrush Fill = new SolidColorBrush(Color.FromRgb(0xf3, 0xf3, 0xf3));
    private static readonly Pen Wave = new(new SolidColorBrush(Color.FromRgb(0xf3, 0xf3, 0xf3)), 1.6);

    static VolumeGlyph() => AffectsRender<VolumeGlyph>(MutedProperty, LevelProperty);

    public VolumeGlyph()
    {
        Width = 18;
        Height = 18;
    }

    public bool Muted
    {
        get => GetValue(MutedProperty);
        set => SetValue(MutedProperty, value);
    }

    public double Level
    {
        get => GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var speaker = new StreamGeometry();
        using (var g = speaker.Open())
        {
            g.BeginFigure(new Point(2, 7), true);
            g.LineTo(new Point(5, 7));
            g.LineTo(new Point(9, 3.5));
            g.LineTo(new Point(9, 14.5));
            g.LineTo(new Point(5, 11));
            g.LineTo(new Point(2, 11));
            g.EndFigure(true);
        }

        context.DrawGeometry(Fill, null, speaker);
        if (Muted)
        {
            context.DrawLine(Wave, new Point(11.5, 6.5), new Point(16.5, 11.5));
            context.DrawLine(Wave, new Point(16.5, 6.5), new Point(11.5, 11.5));
            return;
        }

        var waves = Level <= 0 ? 0 : Level < 0.34 ? 1 : Level < 0.67 ? 2 : 3;
        for (var i = 1; i <= waves; i++)
        {
            var radius = 2.5 * i;
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                g.BeginFigure(new Point(9.5 + (radius * Math.Cos(-0.9)), 9 + (radius * Math.Sin(-0.9))), false);
                g.ArcTo(new Point(9.5 + (radius * Math.Cos(0.9)), 9 + (radius * Math.Sin(0.9))), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
                g.EndFigure(false);
            }

            context.DrawGeometry(null, Wave, geometry);
        }
    }
}
