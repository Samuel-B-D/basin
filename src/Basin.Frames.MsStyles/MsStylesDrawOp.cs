using System.Drawing;
using Basin.Capabilities;
using Basin.Themes.MsStyles;

namespace Basin.Frames.MsStyles;

public struct MsStylesDrawOp
{
    public MsStylesDrawOpKind Kind;
    public int Bitmap;
    public RectangleF Source;
    public Rectangle Frame;
    public RectangleF Destination;
    public float Opacity;
    public bool TileX;
    public bool TileY;
    public float TileScale;
    public bool Smooth;
    public uint Color;
    public float Size;
    public float Radius;
    public BorderType Shape;
    public MsStylesGradient Gradient;
    public string? Text;
    public MsStylesFont Font;
    public float FontPixels;
    public HorizontalAlign Align;
    public uint ShadowColor;
    public PointF ShadowOffset;
    public FrameIcon Icon;
}
