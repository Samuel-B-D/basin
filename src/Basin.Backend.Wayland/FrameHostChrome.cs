using Basin.Capabilities;
using static Basin.Backend.Wayland.WaylandBackendLog;

namespace Basin.Backend.Wayland;

public sealed class FrameHostChrome : HostFrameChrome
{
    private const uint DoubleClickMillis = 400;

    private readonly IFrameRenderer _renderer;
    private readonly Action _onClose;
    private readonly Action<string> _setCursor;

    private FramePart _hot = FramePart.None;
    private FramePart _pressed = FramePart.None;
    private uint _lastTitlePressMs;
    private bool _faulted;

    public FrameHostChrome(
        WaylandHostFrame frame,
        IUIHost host,
        IFrameRenderer renderer,
        string? title = null,
        string appId = "dev.basin.compositor",
        Action? onClose = null,
        Action<string>? setCursor = null)
        : base(frame, host)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        _renderer = renderer;
        FixedTitle = title;
        AppId = appId;
        _onClose = onClose ?? frame.RequestClose;
        _setCursor = setCursor ?? (name => frame.ShowCursor(name));
        Relayout();
    }

    public string? FixedTitle { get; }

    public string AppId { get; }

    public bool Faulted => _faulted;

    protected override void Relayout()
    {
        if (IsDisposed || _faulted)
        {
            return;
        }

        var state = BuildState();
        var insets = _renderer.Measure(state, Frame.Scale);
        if (!Frame.SetInsets(new HostFrameInsets(insets.Top, insets.Right, insets.Bottom, insets.Left)))
        {
            return;
        }

        Repaint(state);
    }

    private void Repaint(in FrameState state)
    {
        if (IsDisposed || _faulted)
        {
            return;
        }

        var outerWidth = Frame.OuterWidth;
        var outerHeight = Frame.OuterHeight;
        var insets = Frame.Insets;
        if (outerWidth <= 0 || outerHeight <= 0 || insets.IsEmpty)
        {
            return;
        }

        try
        {
            if (!EnsureSurface())
            {
                return;
            }

            var hole = new Box(
                insets.Left,
                insets.Top,
                outerWidth - insets.Left - insets.Right,
                outerHeight - insets.Top - insets.Bottom);
            _renderer.Draw(Surface!, hole, state, new FrameInteraction(_hot, _pressed));
            Present();
        }
        catch (Exception e)
        {
            _faulted = true;
            Frame.SetInsets(default);
            Log.Error($"host frame fault: {e.Message}");
        }
    }

    private FrameState BuildState() => new()
    {
        Title = FixedTitle ?? Frame.Title,
        AppId = AppId,
        Active = Frame.Activated,
        Maximized = Frame.Maximized,
        Fullscreen = Frame.Fullscreen,
        Resizing = Frame.Resizing,
        Capabilities = FrameCapabilities.WindowMenu | FrameCapabilities.Maximize | FrameCapabilities.Minimize,
    };

    protected override void OnPointerEnter(double x, double y)
    {
        _hot = FramePart.None;
        UpdateHover(x, y, force: true);
    }

    protected override void OnPointerMotion(uint timeMs, double x, double y) => UpdateHover(x, y, force: false);

    protected override void OnPointerLeave()
    {
        if (_hot == FramePart.None && _pressed == FramePart.None)
        {
            return;
        }

        _hot = FramePart.None;
        _pressed = FramePart.None;
        Repaint(BuildState());
    }

    private void UpdateHover(double x, double y, bool force)
    {
        var part = _renderer.PartAt(x, y, BuildState(), Frame.Scale);
        if (part == _hot && !force)
        {
            return;
        }

        var changed = part != _hot;
        _hot = part;
        _setCursor(_renderer.CursorFor(part) ?? CursorForPart(part));
        if (changed)
        {
            Repaint(BuildState());
        }
    }

    protected override void OnPointerButton(uint timeMs, uint button, bool pressed)
    {
        if (button != InputCodes.BtnLeft)
        {
            return;
        }

        if (!pressed)
        {
            if (_pressed != FramePart.None)
            {
                var released = _pressed;
                _pressed = FramePart.None;
                if (PointerInside && released == _hot)
                {
                    Activate(released);
                }

                Repaint(BuildState());
            }

            return;
        }

        var part = _renderer.PartAt(PointerX, PointerY, BuildState(), Frame.Scale);
        switch (part)
        {
            case FramePart.Title or FramePart.Icon:
                if (timeMs - _lastTitlePressMs <= DoubleClickMillis)
                {
                    _lastTitlePressMs = 0;
                    ToggleMaximize();
                    return;
                }

                _lastTitlePressMs = timeMs;
                Frame.StartMove();
                return;
            case FramePart.TopLeft or FramePart.Top or FramePart.TopRight or
                 FramePart.Right or FramePart.BottomRight or FramePart.Bottom or
                 FramePart.BottomLeft or FramePart.Left:
                Frame.StartResize(EdgesFor(part));
                return;
            case FramePart.None or FramePart.Border:
                return;
            default:
                _pressed = part;
                Repaint(BuildState());
                return;
        }
    }

    private void Activate(FramePart part)
    {
        switch (part)
        {
            case FramePart.Close:
                _onClose();
                break;
            case FramePart.Maximize:
                ToggleMaximize();
                break;
            case FramePart.Minimize:
                Frame.SetMinimized();
                break;
        }
    }

    private void ToggleMaximize() => Frame.SetMaximized(!Frame.Maximized);

    private static HostFrameEdges EdgesFor(FramePart part) => part switch
    {
        FramePart.Top => HostFrameEdges.Top,
        FramePart.Bottom => HostFrameEdges.Bottom,
        FramePart.Left => HostFrameEdges.Left,
        FramePart.Right => HostFrameEdges.Right,
        FramePart.TopLeft => HostFrameEdges.TopLeft,
        FramePart.TopRight => HostFrameEdges.TopRight,
        FramePart.BottomLeft => HostFrameEdges.BottomLeft,
        FramePart.BottomRight => HostFrameEdges.BottomRight,
        _ => HostFrameEdges.None,
    };

    private static string CursorForPart(FramePart part) => part switch
    {
        FramePart.Top => "top_side",
        FramePart.Bottom => "bottom_side",
        FramePart.Left => "left_side",
        FramePart.Right => "right_side",
        FramePart.TopLeft => "top_left_corner",
        FramePart.TopRight => "top_right_corner",
        FramePart.BottomLeft => "bottom_left_corner",
        FramePart.BottomRight => "bottom_right_corner",
        _ => "left_ptr",
    };
}
