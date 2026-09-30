using Basin.Capabilities;

namespace Basin.Backend.Wayland;

public abstract class HostFrameChrome : IDisposable
{
    private int _shownWidth;
    private int _shownHeight;
    private double _shownScale;

    protected HostFrameChrome(WaylandHostFrame frame, IUIHost host)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(host);
        Frame = frame;
        Host = host;
        Frame.StateChanged += HandleStateChanged;
        Frame.PointerEnter += HandlePointerEnter;
        Frame.PointerMotion += HandlePointerMotion;
        Frame.PointerButton += HandlePointerButton;
        Frame.PointerLeave += HandlePointerLeave;
    }

    protected WaylandHostFrame Frame { get; }

    protected IUIHost Host { get; }

    protected IUISurface? Surface { get; private set; }

    protected double PointerX { get; private set; }

    protected double PointerY { get; private set; }

    protected bool PointerInside { get; private set; }

    protected bool IsDisposed { get; private set; }

    public void OnOutputChanged(OutputStateFields fields)
    {
        if (Surface is not null && Frame.OuterWidth == _shownWidth && Frame.OuterHeight == _shownHeight && Frame.Scale == _shownScale)
        {
            return;
        }

        Relayout();
    }

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        Frame.StateChanged -= HandleStateChanged;
        Frame.PointerEnter -= HandlePointerEnter;
        Frame.PointerMotion -= HandlePointerMotion;
        Frame.PointerButton -= HandlePointerButton;
        Frame.PointerLeave -= HandlePointerLeave;
        if (Surface is { } surface)
        {
            OnDisposingSurface(surface);
            surface.Dispose();
            Surface = null;
        }
    }

    protected abstract void Relayout();

    protected virtual void OnStateChanged() => Relayout();

    protected virtual void OnPointerEnter(double x, double y)
    {
    }

    protected virtual void OnPointerMotion(uint timeMs, double x, double y)
    {
    }

    protected virtual void OnPointerButton(uint timeMs, uint button, bool pressed)
    {
    }

    protected virtual void OnPointerLeave()
    {
    }

    protected virtual void OnSurfaceCreated(IUISurface surface)
    {
    }

    protected virtual void OnDisposingSurface(IUISurface surface)
    {
    }

    protected bool EnsureSurface()
    {
        var width = Frame.OuterWidth;
        var height = Frame.OuterHeight;
        var scale = Frame.Scale;
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        if (Surface is null)
        {
            var target = (Host.Produces & UITargetKind.Dmabuf) != 0 ? UITargetKind.Dmabuf : UITargetKind.Memory;
            Surface = Host.CreateSurface(new UISurfaceOptions { Target = target, Width = width, Height = height, Scale = scale });
            if (Surface is null)
            {
                return false;
            }

            OnSurfaceCreated(Surface);
        }
        else if (!Surface.Configure(width, height, scale))
        {
            return false;
        }

        _shownWidth = width;
        _shownHeight = height;
        _shownScale = scale;
        return true;
    }

    protected bool Present()
    {
        if (IsDisposed || Surface is not { } surface || !surface.TryAcquire(out var produced))
        {
            return false;
        }

        using (produced)
        {
            return produced.Buffer is { } buffer && Frame.Attach(buffer, produced.Damage);
        }
    }

    protected void ForgetSurface(IUISurface surface)
    {
        if (ReferenceEquals(surface, Surface))
        {
            Surface = null;
        }
    }

    private void HandleStateChanged() => OnStateChanged();

    private void HandlePointerEnter(double x, double y)
    {
        PointerX = x;
        PointerY = y;
        PointerInside = true;
        OnPointerEnter(x, y);
    }

    private void HandlePointerMotion(uint timeMs, double x, double y)
    {
        PointerX = x;
        PointerY = y;
        OnPointerMotion(timeMs, x, y);
    }

    private void HandlePointerButton(uint timeMs, uint button, bool pressed) => OnPointerButton(timeMs, button, pressed);

    private void HandlePointerLeave()
    {
        PointerInside = false;
        OnPointerLeave();
    }
}
