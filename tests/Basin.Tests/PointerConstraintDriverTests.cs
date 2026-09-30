using Basin.Desktop;
using Xunit;
using Lifetime = Basin.Desktop.Protocol.ZwpPointerConstraintsV1.Lifetime;

namespace Basin.Tests;

public sealed class PointerConstraintDriverTests
{
    [Fact]
    public void A_lock_activates_on_focus_and_holds_the_pointer_where_it_was()
    {
        using var host = new CompositorTestHost();
        using var manager = new PointerConstraintsManager(host.Display, host.Compositor, host.Seat);
        var driver = manager.Driver!;
        var window = MappedToplevel.Map(host, host.Client);
        var locked = 0;
        var proxy = Bind(host).LockPointer(window.Surface, host.Client.Seat!.GetPointer(), null, Lifetime.Persistent);
        proxy.Locked += (_, _) => locked++;
        host.PumpUntil(() => manager.ConstraintFor(window.ServerSurface) is not null);

        host.Seat.Pointer.NotifyMotionAt(1, window.ServerSurface, 10, 12, 110, 212);
        host.PumpUntil(() => locked == 1);
        Assert.True(driver.IsLocked);

        var x = 140.0;
        var y = 230.0;
        Assert.True(driver.Constrain(110, 212, ref x, ref y));
        Assert.Equal((110.0, 212.0), (x, y));
    }

    [Fact]
    public void A_vetoed_surface_keeps_its_constraint_inactive_until_the_veto_lifts()
    {
        using var host = new CompositorTestHost();
        using var manager = new PointerConstraintsManager(host.Display, host.Compositor, host.Seat);
        var driver = manager.Driver!;
        var allowed = false;
        driver.Allow = _ => allowed;
        var window = MappedToplevel.Map(host, host.Client);
        var locked = 0;
        var proxy = Bind(host).LockPointer(window.Surface, host.Client.Seat!.GetPointer(), null, Lifetime.Persistent);
        proxy.Locked += (_, _) => locked++;
        host.PumpUntil(() => manager.ConstraintFor(window.ServerSurface) is not null);

        host.Seat.Pointer.NotifyMotionAt(1, window.ServerSurface, 10, 12, 10, 12);
        host.PumpToClient();
        Assert.Null(driver.Active);
        Assert.Equal(0, locked);

        allowed = true;
        driver.Refresh();
        host.PumpUntil(() => locked == 1);
        Assert.True(driver.IsLocked);
    }

    [Fact]
    public void A_confinement_clamps_motion_into_its_region_and_its_surface()
    {
        using var host = new CompositorTestHost();
        using var manager = new PointerConstraintsManager(host.Display, host.Compositor, host.Seat);
        var driver = manager.Driver!;
        var window = MappedToplevel.Map(host, host.Client, width: 60, height: 50);
        var region = host.Client.Compositor.CreateRegion();
        region.Add(10, 10, 20, 20);
        var confined = 0;
        var proxy = Bind(host).ConfinePointer(window.Surface, host.Client.Seat!.GetPointer(), region, Lifetime.Persistent);
        proxy.Confined += (_, _) => confined++;
        region.Dispose();
        host.PumpUntil(() => manager.ConstraintFor(window.ServerSurface) is not null);

        host.Seat.Pointer.NotifyMotionAt(1, window.ServerSurface, 2, 2, 102, 202);
        host.PumpToClient();
        Assert.Null(driver.Active);

        host.Seat.Pointer.NotifyMotionAt(2, window.ServerSurface, 15, 15, 115, 215);
        host.PumpUntil(() => confined == 1);

        var x = 190.0;
        var y = 205.0;
        Assert.True(driver.Constrain(115, 215, ref x, ref y));
        Assert.Equal(130 - (1.0 / 256), x, 6);
        Assert.Equal(210.0, y, 6);

        var insideX = 120.0;
        var insideY = 220.0;
        Assert.False(driver.Constrain(115, 215, ref insideX, ref insideY));
    }

    [Fact]
    public void A_oneshot_lock_ends_on_focus_loss_and_reports_the_change()
    {
        using var host = new CompositorTestHost();
        using var manager = new PointerConstraintsManager(host.Display, host.Compositor, host.Seat);
        var driver = manager.Driver!;
        var changes = new List<(PointerConstraint? Previous, PointerConstraint? Next)>();
        driver.Changed += (previous, next) => changes.Add((previous, next));
        var window = MappedToplevel.Map(host, host.Client);
        var unlocked = 0;
        var proxy = Bind(host).LockPointer(window.Surface, host.Client.Seat!.GetPointer(), null, Lifetime.Oneshot);
        proxy.Unlocked += (_, _) => unlocked++;
        host.PumpUntil(() => manager.ConstraintFor(window.ServerSurface) is not null);

        host.Seat.Pointer.NotifyMotionAt(1, window.ServerSurface, 5, 5, 5, 5);
        host.Seat.Pointer.NotifyClearFocus();
        host.PumpUntil(() => unlocked == 1);

        Assert.Null(driver.Active);
        Assert.Null(manager.ConstraintFor(window.ServerSurface));
        Assert.Equal(2, changes.Count);
        Assert.Null(changes[0].Previous);
        Assert.Same(changes[0].Next, changes[1].Previous);
        Assert.Null(changes[1].Next);
    }

    private static Basin.Desktop.Protocol.ZwpPointerConstraintsV1 Bind(CompositorTestHost host)
    {
        Basin.Desktop.Protocol.ZwpPointerConstraintsV1? proxy = null;
        var registry = host.Client.Display.GetRegistry();
        registry.Global += (_, e) =>
        {
            if (e.Interface == "zwp_pointer_constraints_v1")
            {
                proxy = registry.Bind<Basin.Desktop.Protocol.ZwpPointerConstraintsV1>(e.Name, 1);
            }
        };
        host.PumpToClient();
        Assert.NotNull(proxy);
        return proxy!;
    }
}
