using Basin.Config;

namespace Basin.WindowManager;

public sealed class WmBindingTable<TMode>
    where TMode : struct, Enum
{
    private readonly RiverWindowManager _wm;
    private readonly TMode _defaultMode;
    private readonly List<(TMode Mode, KeyBinding Binding)> _bindings = [];
    private readonly HashSet<WmSeat> _armed = [];
    private bool _locked;

    public WmBindingTable(RiverWindowManager wm, TMode defaultMode = default)
    {
        ArgumentNullException.ThrowIfNull(wm);
        _wm = wm;
        _defaultMode = defaultMode;
        Mode = defaultMode;
    }

    public bool IsSupported => _wm.Bindings.IsSupported;

    public TMode Mode { get; set; }

    public bool DisableWhileLocked { get; init; } = true;

    public Func<uint, Modifiers, bool>? Shadowed { get; set; }

    public void Arm(ManageContext context, Action<WmSeat> arm)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(arm);
        if (!IsSupported)
        {
            return;
        }

        foreach (var seat in context.Seats)
        {
            if (_armed.Add(seat))
            {
                arm(seat);
            }
        }
    }

    public KeyBinding? Bind(WmSeat seat, string keysym, Modifiers modifiers, Action pressed, TMode? mode = null) =>
        Bind(seat, Keysym.FromName(keysym), modifiers, pressed, mode);

    public KeyBinding? Bind(WmSeat seat, uint keysym, Modifiers modifiers, Action pressed, TMode? mode = null)
    {
        ArgumentNullException.ThrowIfNull(seat);
        ArgumentNullException.ThrowIfNull(pressed);
        var owner = mode ?? _defaultMode;
        if (mode is null && Shadowed?.Invoke(keysym, modifiers) == true)
        {
            return null;
        }

        var binding = _wm.Bindings.Bind(seat, keysym, modifiers, () =>
        {
            pressed();
            _wm.RequestManage();
        });
        Add(owner, binding);
        return binding;
    }

    public KeyBinding BindUnshadowed(WmSeat seat, uint keysym, Modifiers modifiers, Action pressed)
    {
        ArgumentNullException.ThrowIfNull(seat);
        ArgumentNullException.ThrowIfNull(pressed);
        var binding = _wm.Bindings.Bind(seat, keysym, modifiers, () =>
        {
            pressed();
            _wm.RequestManage();
        });
        Add(_defaultMode, binding);
        return binding;
    }

    public KeyBinding BindRelease(WmSeat seat, string keysym, Action released, TMode? mode = null)
    {
        ArgumentNullException.ThrowIfNull(seat);
        ArgumentNullException.ThrowIfNull(released);
        var binding = _wm.Bindings.Bind(seat, keysym, Modifiers.None);
        binding.Released += () =>
        {
            released();
            _wm.RequestManage();
        };
        Add(mode ?? _defaultMode, binding);
        return binding;
    }

    public void Apply(bool locked)
    {
        _locked = locked;
        foreach (var (mode, binding) in _bindings)
        {
            var want = Live(mode);
            if (want && !binding.IsEnabled)
            {
                binding.Enable();
            }
            else if (!want && binding.IsEnabled)
            {
                binding.Disable();
            }
        }
    }

    public void Reset()
    {
        foreach (var (_, binding) in _bindings)
        {
            binding.Destroy();
        }

        _bindings.Clear();
        _armed.Clear();
    }

    private void Add(TMode mode, KeyBinding binding)
    {
        if (Live(mode))
        {
            binding.Enable();
        }

        _bindings.Add((mode, binding));
    }

    private bool Live(TMode mode) =>
        !(_locked && DisableWhileLocked) && EqualityComparer<TMode>.Default.Equals(mode, Mode);
}
