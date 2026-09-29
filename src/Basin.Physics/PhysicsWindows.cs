using Basin.Capabilities;

namespace Basin.Physics;

public sealed class PhysicsWindows : IToplevelCommitObserver, IToplevelObserver, IDisposable
{
    private readonly IToplevelModel _model;
    private readonly Dictionary<ulong, Binding> _bindings = [];
    private bool _disposed;

    public PhysicsWindows(IToplevelModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
        _model.AddObserver(this);
        _model.AddCommitObserver(this);
    }

    public int Count => _bindings.Count;

    public event Action<ulong, PhysicsBody>? Unbound;

    public PhysicsBody Bind(
        PhysicsWorld world,
        ulong toplevelId,
        double x,
        double y,
        PhysicsBodyKind kind,
        in PhysicsMaterial material,
        PhysicsInsets insets = default,
        bool awake = true)
    {
        ArgumentNullException.ThrowIfNull(world);
        Unbind(toplevelId);
        if (!_model.TryGet(toplevelId, out var info))
        {
            return default;
        }

        var (width, height) = SizeOf(info);
        var body = world.CreateBody(
            kind,
            x - insets.Left,
            y - insets.Top,
            width + insets.Horizontal,
            height + insets.Vertical,
            material,
            toplevelId,
            awake);
        _bindings[toplevelId] = new Binding(body, insets, width, height);
        return body;
    }

    public PhysicsBody Rebind(ulong toplevelId, PhysicsBody body)
    {
        if (!_bindings.TryGetValue(toplevelId, out var binding))
        {
            return default;
        }

        _bindings[toplevelId] = binding with { Body = body };
        body.Tag = toplevelId;
        return body;
    }

    public bool TryGet(ulong toplevelId, out PhysicsBody body)
    {
        if (_bindings.TryGetValue(toplevelId, out var binding) && binding.Body.IsValid)
        {
            body = binding.Body;
            return true;
        }

        body = default;
        return false;
    }

    public PhysicsInsets InsetsOf(ulong toplevelId) =>
        _bindings.TryGetValue(toplevelId, out var binding) ? binding.Insets : default;

    public void SetInsets(ulong toplevelId, PhysicsInsets insets)
    {
        if (!_bindings.TryGetValue(toplevelId, out var binding) || binding.Insets == insets)
        {
            return;
        }

        _bindings[toplevelId] = binding with { Insets = insets };
        if (binding.Body.IsValid)
        {
            var pose = binding.Body.Current;
            binding.Body.Resize(binding.Width + insets.Horizontal, binding.Height + insets.Vertical);
            if (pose.Angle == 0)
            {
                binding.Body.Teleport(
                    pose.X + binding.Insets.Left - insets.Left, pose.Y + binding.Insets.Top - insets.Top);
            }
        }
    }

    public PhysicsPose ContentPose(ulong toplevelId, in PhysicsPose pose)
    {
        var insets = InsetsOf(toplevelId);
        return new PhysicsPose(pose.X + insets.Left, pose.Y + insets.Top, pose.Angle, pose.PivotX - insets.Left, pose.PivotY - insets.Top);
    }

    public bool Unbind(ulong toplevelId)
    {
        if (!_bindings.Remove(toplevelId, out var binding))
        {
            return false;
        }

        if (binding.Body.IsValid)
        {
            binding.Body.Destroy();
        }

        Unbound?.Invoke(toplevelId, binding.Body);
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _model.RemoveObserver(this);
        _model.RemoveCommitObserver(this);
        _bindings.Clear();
    }

    void IToplevelCommitObserver.OnToplevelCommitted(ulong toplevelId, in Box damage) => Track(toplevelId);

    void IToplevelObserver.OnToplevelAdded(ulong toplevelId)
    {
    }

    void IToplevelObserver.OnToplevelChanged(ulong toplevelId) => Track(toplevelId);

    void IToplevelObserver.OnToplevelRemoved(ulong toplevelId) => Unbind(toplevelId);

    private void Track(ulong toplevelId)
    {
        if (!_bindings.TryGetValue(toplevelId, out var binding) || !_model.TryGet(toplevelId, out var info))
        {
            return;
        }

        var (width, height) = SizeOf(info);
        if (width <= 0 || height <= 0 || (width == binding.Width && height == binding.Height))
        {
            return;
        }

        _bindings[toplevelId] = binding with { Width = width, Height = height };
        if (binding.Body.IsValid)
        {
            binding.Body.Resize(width + binding.Insets.Horizontal, height + binding.Insets.Vertical);
        }
    }

    private static (int Width, int Height) SizeOf(in ToplevelInfo info) =>
        !info.Geometry.IsEmpty ? (info.Geometry.Width, info.Geometry.Height)
        : info.Surface is { } surface ? (surface.Current.Width, surface.Current.Height)
        : (0, 0);

    private readonly record struct Binding(PhysicsBody Body, PhysicsInsets Insets, int Width, int Height);
}
