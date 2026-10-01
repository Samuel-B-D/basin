using Basin;
using Basin.Backend.Wayland;
using Basin.Capabilities;
using Basin.Desktop;
using Basin.Scene;
using Basin.Seat;

namespace System7Wm;

internal sealed class System7Seat : IDisposable
{
    private readonly Seat _seat;
    private readonly OutputLayout _layout;
    private readonly Scene _scene;
    private readonly CursorController _cursor;
    private readonly LayoutPointer _pointer;
    private readonly UISurfaceRouter _router;
    private readonly Basin.Seat.Backends.SeatBinder _binder;
    private readonly Basin.Seat.Backends.SeatInjector _injector;
    private readonly Basin.Seat.Backends.SeatTouchDriver _touch;
    private readonly PointerRefresh _pointerRefresh;
    private IUISurface? _routeSurface;
    private Route _route;
    private readonly Basin.Seat.Backends.ButtonLatch _latch;
    private readonly Basin.Seat.Backends.ChromeKeyDelivery _keys;
    private double _x;
    private double _y;
    private bool _disposed;

    public System7Seat(
        Basin.Host.BasinHost host,
        BasinServices services,
        OutputLayout layout,
        Scene scene,
        CursorController cursor,
        UISurfaceIndex index,
        Basin.Backend.Libinput.LibinputBackend? input)
    {
        _seat = services.Require<Seat>();
        _layout = layout;
        _scene = scene;
        _cursor = cursor;
        _router = new UISurfaceRouter(scene, index) { ImplicitGrab = true };
        _latch = new Basin.Seat.Backends.ButtonLatch(_seat);
        _keys = new Basin.Seat.Backends.ChromeKeyDelivery(_seat, _router);
        _pointer = new LayoutPointer(layout);
        _pointer.Moved += () => MoveCursor((uint)Environment.TickCount);
        _pointerRefresh = new PointerRefresh(scene, host.Loop, () => MoveCursor((uint)Environment.TickCount));
        _binder = new Basin.Seat.Backends.SeatBinder(_seat, layout, _pointer, cursor);
        _injector = new Basin.Seat.Backends.SeatInjector(_binder, _seat, layout, _pointer)
        {
            DeliverButton = OnButton,
            DeliverKey = OnKey,
        };
        _touch = new Basin.Seat.Backends.SeatTouchDriver(_binder, _seat);
        _touch.Router.HitTester = new Basin.Seat.Backends.SceneTouchHitTester(scene);
        _touch.Router.Chrome = new Basin.Seat.Backends.UISurfaceTouchChrome(_router);
        _seat.Keyboard.SetKeymap(Basin.Seat.SystemKeymap.Read());

        _binder.RelativePointer = services.Find<Basin.Desktop.RelativePointerManager>();
        _binder.Constraints = services.Find<Basin.Desktop.PointerConstraintDriver>();
        _binder.Key += OnKey;
        _binder.ModifiersChanged += OnModifiers;
        _binder.Button += OnButton;
        _binder.Axis += OnAxis;
        _binder.PointerLeft += _router.PointerLeave;
        if (host.Parent is { } parent)
        {
            _binder.BindParent(parent);
        }

        if (input is not null)
        {
            _binder.BindLibinput(input);
        }
    }

    private enum Route
    {
        Seat,

        Shell,

        Chrome,
    }

    public Func<uint, uint, bool, bool>? KeyHook { get; set; }

    public Func<uint, uint, bool, double, double, bool>? PressHook { get; set; }

    public Func<double, double, bool>? GrabMotion { get; set; }

    public Func<uint, uint, bool>? GrabRelease { get; set; }

    public Func<double, double, string?>? CursorHook { get; set; }

    public Action<IUISurface?, Surface?>? Hovered { get; set; }

    public UISurfaceRouter Router => _router;

    public Basin.Seat.Backends.SeatInjector Injector => _injector;

    public Basin.Seat.Backends.SeatBinder Binder => _binder;

    public Seat Seat => _seat;

    public double PointerX => _x;

    public double PointerY => _y;

    public bool Grabbing { get; set; }

    public uint Modifiers { get; private set; }

    public void CenterPointer() => _injector.Center();

    public void Warp(double x, double y) => _pointer.Warp(x, y);

    public void Refresh() => MoveCursor((uint)Environment.TickCount);

    public void Dispose()
    {
        _disposed = true;
        _pointerRefresh.Dispose();
    }

    private void MoveCursor(uint time)
    {
        if (_disposed)
        {
            return;
        }

        _x = _pointer.X;
        _y = _pointer.Y;
        _cursor.MoveTo(_x, _y);
        if (Grabbing)
        {
            GrabMotion?.Invoke(_x, _y);
            return;
        }

        var route = _router.PointerMotion(time, _x, _y);
        if (route.Surface is not null)
        {
            if (route.Entered)
            {
                _seat.Pointer.NotifyClearFocus();
            }

            _cursor.SetHover(null, overClient: false);
            _cursor.ShowNamed(CursorHook?.Invoke(_x, _y) ?? route.Cursor ?? "default");
            Hovered?.Invoke(route.Surface, null);
            return;
        }

        if (_scene.SurfaceAt(_x, _y) is not { } hit || hit.Surface is null)
        {
            _seat.Pointer.NotifyClearFocus();
            _cursor.SetHover(null, overClient: false);
            _cursor.ShowNamed(CursorHook?.Invoke(_x, _y) ?? "default");
            Hovered?.Invoke(null, null);
            return;
        }

        _cursor.SetHover(hit.Surface, overClient: true);
        _seat.Pointer.NotifyMotionAt(time, hit.Surface, hit.X, hit.Y, _x, _y);
        Hovered?.Invoke(null, hit.Surface);
    }

    private void OnButton(uint time, uint button, bool pressed)
    {
        if (pressed)
        {
            if (_latch.Press())
            {
                _route = Choose(time, button);
            }

            Dispatch(time, button, pressed: true);
            return;
        }

        Dispatch(time, button, pressed: false);
        if (_latch.Release())
        {
            _route = Route.Seat;
            _routeSurface = null;
        }
    }

    private Route Choose(uint time, uint button)
    {
        if (PressHook?.Invoke(time, button, true, _x, _y) == true)
        {
            return Route.Shell;
        }

        if (_router.Hovered is { } surface)
        {
            _routeSurface = surface;
            return Route.Chrome;
        }

        return Route.Seat;
    }

    private void Dispatch(uint time, uint button, bool pressed)
    {
        if (!pressed && Grabbing && _route != Route.Shell)
        {
            if (_route == Route.Chrome && _routeSurface is { } surface)
            {
                _router.PointerButton(time, button, pressed, surface);
            }
            else if (_route == Route.Seat)
            {
                _latch.SeatButton(time, button, false);
            }

            GrabRelease?.Invoke(time, button);
            return;
        }

        switch (_route)
        {
            case Route.Shell:
                if (!pressed)
                {
                    GrabRelease?.Invoke(time, button);
                }

                return;
            case Route.Chrome:
                if (_routeSurface is { } target)
                {
                    _router.PointerButton(time, button, pressed, target);
                }

                return;
        }

        _latch.SeatButton(time, button, pressed);
    }

    private void OnAxis(uint time, PointerAxis axis)
    {
        if (_router.PointerAxis(time, axis))
        {
            return;
        }

        _seat.Pointer.NotifyAxis(time, axis);
    }

    private void OnKey(uint time, uint key, bool pressed)
    {
        if (KeyHook?.Invoke(time, key, pressed) == true)
        {
            return;
        }

        _keys.Deliver(time, key, pressed);
    }

    private void OnModifiers()
    {
        var (depressed, latched, locked, group) = _seat.Keyboard.ModifierState;
        Modifiers = depressed;
        _router.Modifiers(depressed, latched, locked, group);
    }
}
