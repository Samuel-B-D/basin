namespace Basin.WindowManager;

public sealed class WmDrag
{
    private Size? _lastProposed;

    private WmDrag(WmWindow window, PointerOperation op, Edges edges, Rect start)
    {
        Window = window;
        Op = op;
        Edges = edges;
        Start = start;
    }

    public WmWindow Window { get; }

    public PointerOperation Op { get; }

    public Edges Edges { get; }

    public Rect Start { get; }

    public bool IsResize => Edges != Edges.None;

    public bool IsActive => !Op.IsEnded && !Window.IsClosed;

    public bool IsReleased => !Op.IsEnded && Op.IsReleased;

    public Size Desired { get; private set; }

    public static WmDrag Begin(WmWindow window, WmSeat seat, Edges edges, Rect start)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(seat);
        var drag = new WmDrag(window, seat.StartPointerOperation(), edges, start);
        if (edges != Edges.None)
        {
            window.InformResizing(true);
        }

        return drag;
    }

    public static Edges Quadrant(Rect window, Point pointer) =>
        (pointer.X < window.X + (window.Width / 2) ? Edges.Left : Edges.Right)
        | (pointer.Y < window.Y + (window.Height / 2) ? Edges.Top : Edges.Bottom);

    public Point MovedTo(Point delta) => new(Start.X + delta.X, Start.Y + delta.Y);

    public Size SizeFor(Point delta)
    {
        var minimum = Window.SizeHint.Minimum;
        var minWidth = Math.Max(minimum.Width, 1);
        var minHeight = Math.Max(minimum.Height, 1);
        var width = Start.Width;
        var height = Start.Height;
        if ((Edges & Edges.Left) != 0)
        {
            width = Math.Max(Start.Width - delta.X, minWidth);
        }
        else if ((Edges & Edges.Right) != 0)
        {
            width = Math.Max(Start.Width + delta.X, minWidth);
        }

        if ((Edges & Edges.Top) != 0)
        {
            height = Math.Max(Start.Height - delta.Y, minHeight);
        }
        else if ((Edges & Edges.Bottom) != 0)
        {
            height = Math.Max(Start.Height + delta.Y, minHeight);
        }

        return new Size(width, height);
    }

    public Point AnchoredAt(Size committed) => new(
        (Edges & Edges.Left) != 0 ? Start.X + (Start.Width - committed.Width) : Start.X,
        (Edges & Edges.Top) != 0 ? Start.Y + (Start.Height - committed.Height) : Start.Y);

    public void Propose(Size size)
    {
        Desired = size;
        if (_lastProposed == size)
        {
            return;
        }

        _lastProposed = size;
        Window.ProposeDimensions(size);
    }

    public void Finish()
    {
        if (IsResize && !Window.IsClosed)
        {
            Window.InformResizing(false);
            if (!Desired.IsEmpty && _lastProposed != Desired)
            {
                Window.ProposeDimensions(Desired);
            }
        }

        Op.End();
    }
}
