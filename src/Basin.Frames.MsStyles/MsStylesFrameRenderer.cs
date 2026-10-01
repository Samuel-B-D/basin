using Basin.Capabilities;
using Basin.UI.Skia;
using Pixman;
using SkiaSharp;

namespace Basin.Frames.MsStyles;

public sealed class MsStylesFrameRenderer : IFrameRenderer
{
    private readonly MsStylesFramePainter _painter;
    private readonly MsStylesResources _resources;

    public MsStylesFrameRenderer(MsStylesFrameStyle style, MsStylesResources resources)
    {
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(resources);
        if (!ReferenceEquals(resources.Images, style.Images))
        {
            throw new ArgumentException("The resources must be built over the style's image cache.", nameof(resources));
        }

        _painter = new MsStylesFramePainter(style) { TextMetrics = resources.Text };
        _resources = resources;
    }

    public Type SurfaceContract => typeof(ISkiaUISurface);

    public MsStylesFramePainter Painter => _painter;

    public bool OpaqueChrome => _painter.LaidOut && _painter.Opaque;

    public FrameInsets Measure(in FrameState state, double scale) => _painter.Measure(state);

    public FrameInsets Outsets(in FrameState state, double scale) => _painter.Outsets(state);

    public void Draw(IUISurface surface, in Box clientBox, in FrameState state, in FrameInteraction interaction)
    {
        var skia = (ISkiaUISurface)surface;
        var scale = surface.Size.Scale;
        _painter.Layout(state, clientBox.Width, clientBox.Height);
        _painter.Paint(state, interaction, scale);
        var canvas = skia.BeginDraw();
        try
        {
            canvas.Clear(SKColors.Transparent);
            canvas.Save();
            canvas.ResetMatrix();
            MsStylesSkiaPainter.Paint(canvas, _painter.List, _resources);
            canvas.Restore();
        }
        finally
        {
            skia.EndDraw();
        }
    }

    public FramePart PartAt(double x, double y, in FrameState state, double scale) => _painter.PartAt(x, y);

    public Box PartBounds(FramePart part) => _painter.PartBounds(part);

    public bool Animating(in FrameState state, in FrameInteraction interaction) => _painter.Animating(state, interaction);

    public bool FollowsPlacement => _painter.Style.IsGlass;

    public bool BackdropRegion(in FrameState state, double scale, PixmanRegion32 into)
    {
        ArgumentNullException.ThrowIfNull(into);
        return _painter.GlassBackdropRegion(into);
    }

    public string? CursorFor(FramePart part) => part switch
    {
        FramePart.Top => "top_side",
        FramePart.Bottom => "bottom_side",
        FramePart.Left => "left_side",
        FramePart.Right => "right_side",
        FramePart.TopLeft => "top_left_corner",
        FramePart.TopRight => "top_right_corner",
        FramePart.BottomLeft => "bottom_left_corner",
        FramePart.BottomRight => "bottom_right_corner",
        _ => null,
    };
}
