using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;
using Basin;
using Basin.Backend.Wayland;
using Basin.Capabilities;
using Basin.UI.Avalonia;
using Pixman;
using System7.Avalonia;
using System7.Avalonia.Controls;
using System7.Avalonia.Rendering;

namespace System7Wm;

internal sealed class HostWindow : IUISurfaceObserver, IDisposable
{
    private const uint LeftButton = 0x110;

    private readonly WaylandHostFrame _frame;
    private readonly AvaloniaUIHost _host;
    private readonly Action _close;
    private readonly System7WindowFrame _window;
    private readonly LayoutTransformControl _zoom;
    private HostFrameInsets _insets;
    private readonly string _title;
    private AvaloniaUISurface? _surface;
    private System7ColorDepth _depth;
    private int _shownWidth;
    private int _shownHeight;
    private double _shownScale;
    private double _pointerX;
    private double _pointerY;
    private bool _forwarding;
    private bool _disposed;

    public HostWindow(WaylandHostFrame frame, AvaloniaUIHost host, string title, System7ColorDepth depth, Action close)
    {
        _frame = frame;
        _host = host;
        _title = title;
        _depth = depth;
        _close = close;
        _window = new System7WindowFrame
        {
            Kind = System7WindowKind.Document,
            Title = title,
            CanClose = true,
            CanZoom = true,
            CanResize = false,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        _zoom = new LayoutTransformControl
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Child = _window,
        };
        _window.CloseRequested += (_, _) => _close();
        _window.ZoomRequested += (_, _) => _frame.SetMaximized(!_frame.Maximized);
        _frame.StateChanged += Relayout;
        _frame.PointerEnter += OnPointerEnter;
        _frame.PointerMotion += OnPointerMotion;
        _frame.PointerButton += OnPointerButton;
        _frame.PointerLeave += OnPointerLeave;
        Relayout();
    }

    public void SetDepth(System7ColorDepth depth)
    {
        _depth = depth;
        if (_surface?.Content is Control root)
        {
            System7Theme.SetColorDepth(root, depth);
        }
    }

    public void OnOutputChanged(OutputStateFields fields)
    {
        if (_surface is null || _shownWidth != _frame.OuterWidth || _shownHeight != _frame.OuterHeight || _shownScale != _frame.Scale)
        {
            Relayout();
        }
    }

    public void OnSurfaceDamaged(IUISurface surface, PixmanRegion32 damage)
    {
        if (_disposed || !ReferenceEquals(surface, _surface) || !surface.TryAcquire(out var produced))
        {
            return;
        }

        using (produced)
        {
            if (produced.Buffer is { } buffer)
            {
                _frame.Attach(buffer, produced.Damage);
            }
        }
    }

    public void OnSurfaceDestroyed(IUISurface surface)
    {
        if (ReferenceEquals(surface, _surface))
        {
            _surface = null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _frame.StateChanged -= Relayout;
        _frame.PointerEnter -= OnPointerEnter;
        _frame.PointerMotion -= OnPointerMotion;
        _frame.PointerButton -= OnPointerButton;
        _frame.PointerLeave -= OnPointerLeave;
        if (_surface is { } surface)
        {
            surface.RemoveObserver(this);
            surface.Dispose();
            _surface = null;
        }
    }

    private void Relayout()
    {
        if (_disposed)
        {
            return;
        }

        var chrome = DevicePlacement.Chrome(_frame.Scale);
        var zoom = chrome / _frame.Scale;
        int Fit(int dip, int slack) => (int)Math.Ceiling(((dip * chrome) + slack) / _frame.Scale - 1e-9);
        var slack = chrome == _frame.Scale ? 0 : chrome - 1;
        var insets = _frame.Fullscreen ? default : new HostFrameInsets(Fit(ClientFrame.TitleHeight, 0), Fit(2, slack), Fit(2, slack), Fit(1, 0));
        _insets = insets;
        if (!_frame.SetInsets(insets) || insets.IsEmpty)
        {
            return;
        }

        _window.IsActive = _frame.Activated;
        var width = _frame.OuterWidth;
        var height = _frame.OuterHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        _zoom.LayoutTransform = new Avalonia.Media.ScaleTransform(zoom, zoom);
        _window.Width = Math.Floor(width / zoom);
        _window.Height = Math.Floor(height / zoom);

        if (_surface is null)
        {
            var target = (_host.Produces & UITargetKind.Dmabuf) != 0 ? UITargetKind.Dmabuf : UITargetKind.Memory;
            _surface = _host.CreateSurface(new UISurfaceOptions { Target = target, Width = width, Height = height, Scale = _frame.Scale }) as AvaloniaUISurface;
            if (_surface is null)
            {
                return;
            }

            _surface.AddObserver(this);
            var root = new Panel { Background = null, Children = { _zoom } };
            root.Styles.Add(new Style(x => x.Is<Layoutable>())
            {
                Setters = { new Setter(Layoutable.UseLayoutRoundingProperty, false) },
            });
            System7Theme.SetColorDepth(root, _depth);
            _surface.Content = root;
        }
        else
        {
            _surface.Configure(width, height, _frame.Scale);
        }

        _shownWidth = width;
        _shownHeight = height;
        _shownScale = _frame.Scale;
    }

    private System7WindowPart PartAt(double x, double y)
    {
        var zoom = DevicePlacement.Chrome(_frame.Scale) / _frame.Scale;
        return System7WindowGeometry.HitTest(System7WindowKind.Document, (int)_window.Width, (int)_window.Height,
            (int)(x / zoom), (int)(y / zoom), _frame.Activated, true, true, false);
    }

    private HostFrameEdges EdgesAt(double x, double y)
    {
        var right = x >= _frame.OuterWidth - _insets.Right;
        var bottom = y >= _frame.OuterHeight - _insets.Bottom;
        var nearRight = x >= _frame.OuterWidth - 16;
        var nearBottom = y >= _frame.OuterHeight - 16;
        return (right && nearBottom) || (bottom && nearRight) ? HostFrameEdges.BottomRight
            : right ? HostFrameEdges.Right
            : bottom ? HostFrameEdges.Bottom
            : HostFrameEdges.None;
    }

    private void OnPointerEnter(double x, double y)
    {
        _pointerX = x;
        _pointerY = y;
        _surface?.NotifyPointerEnter(x, y);
    }

    private void OnPointerMotion(uint timeMs, double x, double y)
    {
        _pointerX = x;
        _pointerY = y;
        _surface?.NotifyPointerMotion(timeMs, x, y);
    }

    private void OnPointerLeave()
    {
        _forwarding = false;
        _surface?.NotifyPointerLeave();
    }

    private void OnPointerButton(uint timeMs, uint button, bool pressed)
    {
        if (button != LeftButton)
        {
            return;
        }

        if (!pressed)
        {
            if (_forwarding)
            {
                _forwarding = false;
                _surface?.NotifyPointerButton(timeMs, button, false);
            }

            return;
        }

        if (EdgesAt(_pointerX, _pointerY) is var edges and not HostFrameEdges.None)
        {
            _frame.StartResize(edges);
            return;
        }

        switch (PartAt(_pointerX, _pointerY))
        {
            case System7WindowPart.Close or System7WindowPart.Zoom:
                _forwarding = true;
                _surface?.NotifyPointerButton(timeMs, button, true);
                break;
            case System7WindowPart.TitleBar:
                _frame.StartMove();
                break;
        }
    }
}
