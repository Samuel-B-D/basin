using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace LunaWm;

public sealed class BluetoothGlyph : Control
{
    public static readonly StyledProperty<bool> ConnectedProperty =
        AvaloniaProperty.Register<BluetoothGlyph, bool>(nameof(Connected));

    private static readonly Pen Rune = new(new SolidColorBrush(Color.FromRgb(0xf3, 0xf3, 0xf3)), 1.6, lineJoin: PenLineJoin.Miter);
    private static readonly IBrush Dot = new SolidColorBrush(Color.FromRgb(0xf3, 0xf3, 0xf3));

    static BluetoothGlyph() => AffectsRender<BluetoothGlyph>(ConnectedProperty);

    public BluetoothGlyph()
    {
        Width = 13;
        Height = 18;
    }

    public bool Connected
    {
        get => GetValue(ConnectedProperty);
        set => SetValue(ConnectedProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var rune = new StreamGeometry();
        using (var g = rune.Open())
        {
            g.BeginFigure(new Point(3.2, 6.3), false);
            g.LineTo(new Point(9.8, 12.6));
            g.LineTo(new Point(6.5, 15.6));
            g.LineTo(new Point(6.5, 2.4));
            g.LineTo(new Point(9.8, 5.4));
            g.LineTo(new Point(3.2, 11.7));
            g.EndFigure(false);
        }

        context.DrawGeometry(null, Rune, rune);
        if (Connected)
        {
            context.DrawEllipse(Dot, null, new Point(1.5, 9), 1.2, 1.2);
            context.DrawEllipse(Dot, null, new Point(11.5, 9), 1.2, 1.2);
        }
    }
}
