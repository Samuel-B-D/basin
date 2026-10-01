using System.Drawing;
using Basin.Capabilities;
using Basin.Themes.MsStyles;

namespace Basin.Frames.MsStyles;

public sealed class MsStylesDrawList
{
    private MsStylesDrawOp[] _ops = new MsStylesDrawOp[64];

    public int Count { get; private set; }

    public ref readonly MsStylesDrawOp this[int index] => ref _ops[index];

    public ReadOnlySpan<MsStylesDrawOp> Ops => _ops.AsSpan(0, Count);

    public void Clear()
    {
        Array.Clear(_ops, 0, Count);
        Count = 0;
    }

    public void Image(int bitmap, Rectangle frame, RectangleF source, RectangleF destination, float opacity = 1f, bool tileX = false, bool tileY = false, float tileScale = 1f, bool? smooth = null)
    {
        if (destination.Width <= 0 || destination.Height <= 0 || source.Width <= 0 || source.Height <= 0 || opacity <= 0)
        {
            return;
        }

        ref var op = ref Add(MsStylesDrawOpKind.Image);
        op.Bitmap = bitmap;
        op.Source = source;
        op.Frame = frame;
        op.Destination = destination;
        op.Opacity = opacity;
        op.TileX = tileX;
        op.TileY = tileY;
        op.TileScale = tileScale;
        op.Smooth = smooth ?? !(IsIntegral(tileScale) && tileScale >= 1);
    }

    public void Fill(RectangleF destination, uint argb)
    {
        if (destination.Width <= 0 || destination.Height <= 0 || (argb >> 24) == 0)
        {
            return;
        }

        ref var op = ref Add(MsStylesDrawOpKind.Fill);
        op.Destination = destination;
        op.Color = argb;
        op.Opacity = 1f;
    }

    public void Border(RectangleF destination, float size, uint argb, BorderType shape = BorderType.Rect, float radius = 0)
    {
        if (destination.Width <= 0 || destination.Height <= 0 || size <= 0)
        {
            return;
        }

        ref var op = ref Add(MsStylesDrawOpKind.Border);
        op.Destination = destination;
        op.Size = size;
        op.Color = argb;
        op.Shape = shape;
        op.Radius = radius;
        op.Opacity = 1f;
    }

    public void Gradient(RectangleF destination, in MsStylesGradient gradient, BorderType shape = BorderType.Rect, float radius = 0)
    {
        if (destination.Width <= 0 || destination.Height <= 0 || gradient.Count == 0)
        {
            return;
        }

        ref var op = ref Add(MsStylesDrawOpKind.Gradient);
        op.Destination = destination;
        op.Gradient = gradient;
        op.Shape = shape;
        op.Radius = radius;
        op.Opacity = 1f;
    }

    public void Text(string text, in MsStylesFont font, float pixels, RectangleF destination, HorizontalAlign align, uint argb, uint shadowArgb = 0, PointF shadowOffset = default)
    {
        if (string.IsNullOrEmpty(text) || destination.Width <= 0 || destination.Height <= 0)
        {
            return;
        }

        ref var op = ref Add(MsStylesDrawOpKind.Text);
        op.Text = text;
        op.Font = font;
        op.FontPixels = pixels;
        op.Destination = destination;
        op.Align = align;
        op.Color = argb;
        op.ShadowColor = shadowArgb;
        op.ShadowOffset = shadowOffset;
        op.Opacity = 1f;
    }

    public void Glow(string text, in MsStylesFont font, float pixels, RectangleF destination, HorizontalAlign align, uint argb, float size, float intensity)
    {
        if (string.IsNullOrEmpty(text) || destination.Width <= 0 || destination.Height <= 0 || size <= 0)
        {
            return;
        }

        ref var op = ref Add(MsStylesDrawOpKind.Glow);
        op.Text = text;
        op.Font = font;
        op.FontPixels = pixels;
        op.Destination = destination;
        op.Align = align;
        op.Color = argb;
        op.Size = size;
        op.Radius = intensity;
        op.Opacity = 1f;
    }

    public void Icon(RectangleF destination, in FrameIcon icon, string? appId, float opacity = 1f)
    {
        if (destination.Width <= 0 || destination.Height <= 0 || (icon.Pixels is null && string.IsNullOrEmpty(icon.Name) && string.IsNullOrEmpty(appId)))
        {
            return;
        }

        ref var op = ref Add(MsStylesDrawOpKind.Icon);
        op.Destination = destination;
        op.Icon = icon;
        op.Text = appId;
        op.Opacity = opacity;
    }

    public void Clip(RectangleF destination)
    {
        ref var op = ref Add(MsStylesDrawOpKind.Clip);
        op.Destination = destination;
    }

    public void Restore() => Add(MsStylesDrawOpKind.Restore);

    private ref MsStylesDrawOp Add(MsStylesDrawOpKind kind)
    {
        if (Count == _ops.Length)
        {
            Array.Resize(ref _ops, _ops.Length * 2);
        }

        ref var op = ref _ops[Count++];
        op = default;
        op.Kind = kind;
        return ref op;
    }

    private static bool IsIntegral(float value) => Math.Abs(value - MathF.Round(value)) < 0.001f;
}
