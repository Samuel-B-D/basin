using System.Drawing;
using Basin.Capabilities;

namespace Basin.Frames.MsStyles;

public sealed class MsStylesFrameGeometry
{
    private readonly MsStylesFrameButton[] _buttons = new MsStylesFrameButton[3];

    public int Width { get; internal set; }

    public int Height { get; internal set; }

    public FrameInsets Insets { get; internal set; }

    public FrameInsets Outsets { get; internal set; }

    public RectangleF WindowRect { get; internal set; }

    public RectangleF ClientRect { get; internal set; }

    public int Border { get; internal set; }

    public int CaptionHeight { get; internal set; }

    public bool Maximized { get; internal set; }

    public bool Small { get; internal set; }

    public bool HasCaption { get; internal set; }

    public RectangleF CaptionRect { get; internal set; }

    public RectangleF LeftRect { get; internal set; }

    public RectangleF RightRect { get; internal set; }

    public RectangleF BottomRect { get; internal set; }

    public RectangleF IconRect { get; internal set; }

    public RectangleF TitleRect { get; internal set; }

    public int ButtonCount { get; internal set; }

    public ReadOnlySpan<MsStylesFrameButton> Buttons => _buttons.AsSpan(0, ButtonCount);

    internal ref MsStylesFrameButton Button(int index) => ref _buttons[index];

    public FramePart ButtonAt(double x, double y)
    {
        foreach (ref readonly var button in Buttons)
        {
            if (button.Enabled && x >= button.Bounds.Left && x < button.Bounds.Right && y >= button.Bounds.Top && y < button.Bounds.Bottom)
            {
                return button.Part;
            }
        }

        return FramePart.None;
    }

    public RectangleF BoundsOf(FramePart part)
    {
        foreach (ref readonly var button in Buttons)
        {
            if (button.Part == part)
            {
                return button.Bounds;
            }
        }

        return part switch
        {
            FramePart.Title => TitleRect,
            FramePart.Menu or FramePart.Icon => IconRect,
            _ => RectangleF.Empty,
        };
    }
}
