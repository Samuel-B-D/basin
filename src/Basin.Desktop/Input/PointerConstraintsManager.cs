using Basin.Desktop.Protocol;
using Wayland;
using Wayland.Server;

namespace Basin.Desktop;

public sealed class PointerConstraintsManager : IDisposable
{
    public const int Version = 1;

    private readonly WlGlobal _global;
    private readonly CompositorGlobal _compositor;
    private readonly Dictionary<Surface, PointerConstraint> _bySurface = [];

    public PointerConstraintsManager(WlServerDisplay display, CompositorGlobal compositor, Seat.Seat? seat = null)
    {
        _compositor = compositor;
        _global = display.CreateGlobal(ZwpPointerConstraintsV1.Interface, Version, OnBind);
        if (seat is not null)
        {
            Driver = new PointerConstraintDriver(this, seat);
        }
    }

    public PointerConstraintDriver? Driver { get; }

    public event Action<PointerConstraint>? ConstraintCreated;

    public event Action<PointerConstraint>? ConstraintActivated;

    public event Action<PointerConstraint>? ConstraintRemoved;

    public PointerConstraint? ConstraintFor(Surface surface) =>
        _bySurface.TryGetValue(surface, out var constraint) ? constraint : null;

    public void Dispose()
    {
        Driver?.Dispose();
        var live = new PointerConstraint[_bySurface.Count];
        _bySurface.Values.CopyTo(live, 0);
        foreach (var constraint in live)
        {
            constraint.Teardown();
        }

        _bySurface.Clear();
        _global.Dispose();
    }

    internal Region? ResolveRegion(WlRegionResource? resource) => _compositor.ResolveRegion(resource);

    private void OnBind(WlClient client, uint version, uint id)
    {
        var manager = new ZwpPointerConstraintsV1Resource(client, version, id);
        manager.LockPointer += (_, e) => Create(client, manager.Version, e.Id, e.Surface, e.Region, ConstraintKind.Lock, (uint)e.Lifetime);
        manager.ConfinePointer += (_, e) => Create(client, manager.Version, e.Id, e.Surface, e.Region, ConstraintKind.Confine, (uint)e.Lifetime);
    }

    private void Create(WlClient client, uint version, uint id, WlSurfaceResource? surfaceResource, WlRegionResource? region, ConstraintKind kind, uint lifetime)
    {
        var surface = _compositor.ResolveSurface(surfaceResource);
        if (surface is null)
        {
            return;
        }

        if (_bySurface.ContainsKey(surface))
        {
            if (kind == ConstraintKind.Lock)
            {
                new ZwpLockedPointerV1Resource(client, version, id).PostError(
                    (uint)ZwpPointerConstraintsV1.Error.AlreadyConstrained, "surface already constrained");
            }
            else
            {
                new ZwpConfinedPointerV1Resource(client, version, id).PostError(
                    (uint)ZwpPointerConstraintsV1.Error.AlreadyConstrained, "surface already constrained");
            }

            return;
        }

        var constraint = new PointerConstraint(this, surface, kind, lifetime == 2, _compositor.ResolveRegion(region), client, version, id);
        _bySurface[surface] = constraint;
        ConstraintCreated?.Invoke(constraint);
        Driver?.Refresh();
    }

    internal void NotifyActivated(PointerConstraint constraint) => ConstraintActivated?.Invoke(constraint);

    internal void Remove(PointerConstraint constraint)
    {
        if (_bySurface.TryGetValue(constraint.Surface, out var held) && held == constraint)
        {
            _bySurface.Remove(constraint.Surface);
        }

        ConstraintRemoved?.Invoke(constraint);
    }
}
