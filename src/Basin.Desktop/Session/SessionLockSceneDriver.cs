using Basin.Scene;

namespace Basin.Desktop;

public sealed class SessionLockSceneDriver : ISessionLockDriver
{
    private readonly BasinServices? _services;
    private readonly SceneTree _lockTree;
    private readonly OutputLayout _layout;
    private readonly Action<bool> _setLocked;
    private readonly List<(LockSurface Lock, SceneSurface Scene)> _surfaces = [];
    private Seat.Seat? _seat;
    private SessionLockManager? _manager;

    public SessionLockSceneDriver(
        SessionLockManager manager,
        Seat.Seat seat,
        SceneTree lockTree,
        OutputLayout layout,
        Action<bool> setLocked)
        : this(lockTree, layout, setLocked)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(seat);
        _seat = seat;
        AttachTo(manager);
    }

    public SessionLockSceneDriver(
        BasinServices services,
        SceneTree lockTree,
        OutputLayout layout,
        Action<bool> setLocked)
        : this(lockTree, layout, setLocked)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        services.Use<ISessionLockDriver>(this);
    }

    private SessionLockSceneDriver(SceneTree lockTree, OutputLayout layout, Action<bool> setLocked)
    {
        ArgumentNullException.ThrowIfNull(lockTree);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(setLocked);
        _lockTree = lockTree;
        _layout = layout;
        _setLocked = setLocked;
    }

    public TextInputManager? TextInput { get; set; }

    public event Action? Locked;

    public event Action? Unlocked;

    public event Action? Abandoned;

    public event Action<LockSurface, SceneSurface>? LockSurfaceAdded;

    public void LockNow()
    {
        OnLockRequested();
        Locked?.Invoke();
    }

    public void UnlockNow() => OnUnlocked();

    void ISessionLockDriver.Attach(SessionLockManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);
        AttachTo(manager);
    }

    public void Reconfigure()
    {
        foreach (var (lockSurface, lockScene) in _surfaces)
        {
            var box = _layout.BoxOf(lockSurface.Output.Output);
            lockScene.Tree.SetPosition(box.X, box.Y);
            lockSurface.Configure(box.Width, box.Height);
        }
    }

    private Seat.Seat LockSeat => _seat ??= _services!.Require<Seat.Seat>();

    private void AttachTo(SessionLockManager manager)
    {
        if (_manager is not null)
        {
            throw new InvalidOperationException("this session-lock driver already drives a lock manager");
        }

        _manager = manager;
        manager.LockRequested += OnLockRequested;
        manager.Locked += () => Locked?.Invoke();
        manager.Unlocked += OnUnlocked;
        manager.Abandoned += () => Abandoned?.Invoke();
        manager.NewLockSurface += OnNewLockSurface;
    }

    private void OnLockRequested()
    {
        _setLocked(true);
        LockSeat.Keyboard.NotifyClearFocus();
        TextInput?.NotifyFocus(null);
        LockSeat.Pointer.NotifyClearFocus();
    }

    private void OnUnlocked()
    {
        foreach (var (_, lockScene) in _surfaces)
        {
            if (!lockScene.IsDestroyed)
            {
                lockScene.Destroy();
            }
        }

        _surfaces.Clear();
        _setLocked(false);
        Unlocked?.Invoke();
    }

    private void OnNewLockSurface(LockSurface lockSurface)
    {
        var lockScene = new SceneSurface(_lockTree, lockSurface.Surface);
        var box = _layout.BoxOf(lockSurface.Output.Output);
        lockScene.Tree.SetPosition(box.X, box.Y);
        _surfaces.Add((lockSurface, lockScene));
        lockSurface.Mapped += () => LockSeat.Keyboard.NotifyEnter(lockSurface.Surface);
        lockSurface.Unmapped += () =>
        {
            var index = _surfaces.FindIndex(entry => entry.Lock == lockSurface);
            if (index < 0)
            {
                return;
            }

            _surfaces[index].Scene.Destroy();
            _surfaces.RemoveAt(index);
        };
        LockSurfaceAdded?.Invoke(lockSurface, lockScene);
    }
}
