using Basin;
using Basin.Backend.Wayland;
using Basin.Capabilities;
using Basin.Desktop;
using Basin.Scene;
using Basin.Seat;

namespace System7Wm;

internal sealed class System7Seat : IDisposable, ITouchChrome
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
    private int _buttonsDown;
    private bool _seatHoldsButton;
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
        _pointer = new LayoutPointer(layout);
        _pointer.Moved += () => MoveCursor((uint)Environment.TickCount);
        _pointerRefresh = new PointerRefresh(scene, host.Loop, () => MoveCursor((uint)Environment.TickCount));
        _binder = new Basin.Seat.Backends.SeatBinder(_seat, layout, _pointer, cursor);
        _injector = new Basin.Seat.Backends.SeatInjector(_binder, _seat, layout, _pointer)
        {
            Moved = MoveCursor,
            DeliverButton = OnButton,
            DeliverKey = OnKey,
        };
        _touch = new Basin.Seat.Backends.SeatTouchDriver(_binder, _seat);
        _touch.Router.HitTester = new Basin.Seat.Backends.SceneTouchHitTester(scene);
        _touch.Router.Chrome = this;
        _seat.Keyboard.SetKeymap(Basin.Seat.SystemKeymap.Read());

        if (host.Parent is { } parent)
        {
            parent.PointerAdded += WireParentPointer;
            parent.KeyboardAdded += WireParentKeyboard;
            _binder.BindParentTouch(parent);
        }

        if (input is not null)
        {
            WireLibinput(input);
            _binder.BindLibinputTouch(input);
            input.Start();
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

    bool ITouchChrome.TryPress(int id, uint timeMs, double x, double y) => _router.TouchDown(timeMs, id, x, y);

    void ITouchChrome.Motion(int id, uint timeMs, double x, double y) => _router.TouchMotion(timeMs, id, x, y);

    void ITouchChrome.Release(int id, uint timeMs, double x, double y) => _router.TouchUp(timeMs, id);

    void ITouchChrome.Cancel() => _router.TouchCancel();

    private void WireParentPointer(WaylandPointerDevice pointer)
    {
        _cursor.AttachParent(pointer);
        pointer.Enter += (output, x, y) =>
        {
            var (layoutX, layoutY) = _layout.ToLayout(output, x, y);
            _pointer.Warp(layoutX, layoutY);
        };
        pointer.Motion += (time, x, y) =>
        {
            var outputs = _layout.Outputs;
            var output = _layout.OutputAt(_x, _y) ?? (outputs.Length > 0 ? outputs[0].Output : null);
            if (output is null)
            {
                return;
            }

            var (layoutX, layoutY) = _layout.ToLayout(output, x, y);
            _pointer.Warp(layoutX, layoutY);
            MoveCursor(time);
        };
        pointer.Button += (time, button, pressed) => OnButton(time, button, pressed);
        pointer.Axis += (time, axis) => OnAxis(time, axis);
        pointer.Leave += _router.PointerLeave;
    }

    private void WireParentKeyboard(WaylandKeyboardDevice keyboard)
    {
        keyboard.Keymap += bytes => _seat.Keyboard.SetKeymapFromBuffer(bytes);
        keyboard.Key += (time, key, pressed) =>
        {
            _seat.Keyboard.Activate(null);
            OnKey(time, key, pressed);
        };
        keyboard.Modifiers += OnModifiers;
    }

    private void WireLibinput(Basin.Backend.Libinput.LibinputBackend input)
    {
        input.Key += (device, time, key, pressed) =>
        {
            _binder.ActivateKeyboard(device);
            OnKey(time, key, pressed);
        };
        input.PointerButton += (_, time, button, pressed) => OnButton(time, button, pressed);
        input.PointerMotion += (_, time, dx, dy, _, _) =>
        {
            _pointer.Motion(dx, dy);
            MoveCursor(time);
        };
        input.PointerMotionAbsolute += (device, time, nx, ny) =>
        {
            _pointer.MotionAbsolute(_binder.OutputFor(device), nx, ny);
            MoveCursor(time);
        };
        input.PointerScroll += (device, time, axis) => OnAxis(time, _binder.Scale(device, axis));
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
            if (_buttonsDown++ == 0)
            {
                _route = Choose(time, button);
            }

            Dispatch(time, button, pressed: true);
            return;
        }

        if (_buttonsDown > 0)
        {
            _buttonsDown--;
        }

        Dispatch(time, button, pressed: false);
        if (_buttonsDown == 0)
        {
            _route = Route.Seat;
            _routeSurface = null;
            _seatHoldsButton = false;
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
            else if (_route == Route.Seat && _seatHoldsButton)
            {
                _seat.Pointer.NotifyButton(time, button, false);
                _seatHoldsButton = false;
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

        if (pressed)
        {
            _seat.Pointer.NotifyButton(time, button, true);
            _seatHoldsButton = true;
            return;
        }

        if (_seatHoldsButton)
        {
            _seat.Pointer.NotifyButton(time, button, false);
            _seatHoldsButton = false;
        }
    }

    private void OnAxis(uint time, PointerAxis axis)
    {
        var horizontal = axis.Axis == Wayland.WlPointer.Axis.HorizontalScroll;
        if (_router.PointerAxis(time, horizontal ? axis.Value : 0, horizontal ? 0 : axis.Value))
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

        if (_router.Key(time, key, pressed))
        {
            if (pressed)
            {
                Span<char> text = stackalloc char[16];
                var length = _seat.Keyboard.TextFor(key, text);
                if (length > 0)
                {
                    _router.TextCommit(text[..length]);
                }
            }

            _seat.Keyboard.NotifyKeyConsumed(key, pressed);
            return;
        }

        _seat.Keyboard.NotifyKey(time, key, pressed);
    }

    private void OnModifiers(uint depressed, uint latched, uint locked, uint group)
    {
        Modifiers = depressed;
        _router.Modifiers(depressed, latched, locked, group);
        _seat.Keyboard.NotifyModifiers(depressed, latched, locked, group);
    }
}
