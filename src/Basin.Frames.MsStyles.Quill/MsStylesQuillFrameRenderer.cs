using Basin.Capabilities;
using Basin.UI.Quill;
using Pixman;

namespace Basin.Frames.MsStyles.Quill;

public sealed class MsStylesQuillFrameRenderer : IFrameRenderer
{
    private readonly MsStylesFramePainter _painter;
    private readonly MsStylesQuillPainter _quill;

    public MsStylesQuillFrameRenderer(MsStylesFrameStyle style, MsStylesQuillPainter painter)
    {
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(painter);
        if (!ReferenceEquals(painter.Images, style.Images))
        {
            throw new ArgumentException("The painter must be built over the style's image cache.", nameof(painter));
        }

        _painter = new MsStylesFramePainter(style) { TextMetrics = painter.Text };
        _quill = painter;
    }

    public Type SurfaceContract => typeof(IQuillUISurface);

    public MsStylesFramePainter Painter => _painter;

    public bool OpaqueChrome => _painter.LaidOut && _painter.Opaque;

    public FrameInsets Measure(in FrameState state, double scale) => _painter.Measure(state);

    public FrameInsets Outsets(in FrameState state, double scale) => _painter.Outsets(state);

    public void Draw(IUISurface surface, in Box clientBox, in FrameState state, in FrameInteraction interaction)
    {
        ArgumentNullException.ThrowIfNull(surface);
        var quill = (IQuillUISurface)surface;
        var scale = surface.Size.Scale;
        var canvas = quill.BeginDraw();
        try
        {
            _quill.Text.Canvas = canvas;
            _painter.Layout(state, clientBox.Width, clientBox.Height);
            _painter.Paint(state, interaction, scale);
            _quill.Paint(canvas, quill.Renderer, _painter.List, (float)scale);
        }
        finally
        {
            _quill.Text.Canvas = null;
            quill.EndDraw();
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
