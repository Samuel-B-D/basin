using Basin.Desktop;
using Basin.Scene;
using Basin.Shell.Xdg;
using Xunit;

namespace Basin.Tests;

public sealed class SceneDriverObligationTests
{
    public static TheoryData<string, Type> SceneDriven => new()
    {
        { "zwlr_layer_shell_v1", typeof(ILayerShellDriver) },
        { "ext_session_lock_manager_v1", typeof(ISessionLockDriver) },
    };

    [Theory]
    [MemberData(nameof(SceneDriven))]
    public void An_undriven_scene_protocol_refuses_to_freeze(string wireInterface, Type driver)
    {
        using var host = new CompositorTestHost();
        using var services = Registry(host).Install(ModuleFor(wireInterface));

        var error = Assert.Throws<InvalidOperationException>(() => services.Freeze());

        Assert.Contains(wireInterface, error.Message, StringComparison.Ordinal);
        Assert.Contains(driver.Name, error.Message, StringComparison.Ordinal);
        Assert.Contains($"Without(\"{wireInterface}\")", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(SceneDriven))]
    public void Subtracting_a_scene_protocol_removes_the_obligation_and_the_global(string wireInterface, Type driver)
    {
        using var host = new CompositorTestHost();
        using var services = Registry(host)
            .Without(wireInterface)
            .Install(ModuleFor(wireInterface))
            .Freeze();

        Assert.False(services.Modules.ContainsKey(wireInterface));
        Assert.DoesNotContain(driver, services.UnresolvedCapabilities);
        var globals = new List<string>();
        var registry = host.Client.Display.GetRegistry();
        registry.Global += (_, e) => globals.Add(e.Interface);
        host.PumpToClient();
        Assert.DoesNotContain(wireInterface, globals);
    }

    [Theory]
    [MemberData(nameof(SceneDriven))]
    public void A_scene_driver_made_before_freeze_satisfies_the_obligation(string wireInterface, Type driver)
    {
        using var host = new CompositorTestHost();
        var layers = new SceneLayers(host.Scene.Root);
        using var services = Registry(host);
        object made = wireInterface == "zwlr_layer_shell_v1"
            ? new LayerShellSceneDriver(services, host.Layout, layers)
            : new SessionLockSceneDriver(services, layers.Lock, host.Layout, layers.SetLocked);
        services.Install(ModuleFor(wireInterface)).Freeze();

        Assert.True(services.Modules.ContainsKey(wireInterface));
        Assert.IsAssignableFrom(driver, made);
    }

    [Fact]
    public void A_lock_driver_made_before_freeze_blanks_and_maps_a_real_locker()
    {
        using var host = new CompositorTestHost();
        var layers = new SceneLayers(host.Scene.Root);
        using var services = Registry(host);
        var driver = new SessionLockSceneDriver(services, layers.Lock, host.Layout, layers.SetLocked);
        SceneSurface? lockScene = null;
        driver.LockSurfaceAdded += (_, scene) => lockScene = scene;
        services.Install(new SessionLockModule()).Freeze();

        var locker = host.ConnectClient();
        Basin.Desktop.Protocol.ExtSessionLockManagerV1? manager = null;
        var registry = locker.Display.GetRegistry();
        registry.Global += (_, e) =>
        {
            if (e.Interface == "ext_session_lock_manager_v1")
            {
                manager = registry.Bind<Basin.Desktop.Protocol.ExtSessionLockManagerV1>(e.Name, 1);
            }
        };
        host.PumpToClient();
        Assert.NotNull(manager);

        var lockProxy = manager!.Lock();
        host.PumpUntil(() => !layers.Windows.Enabled);

        var surface = locker.Compositor.CreateSurface();
        var lockSurfaceProxy = lockProxy.GetLockSurface(surface, locker.Outputs[0]);
        var size = (W: 0, H: 0);
        lockSurfaceProxy.Configure += (_, e) =>
        {
            size = ((int)e.Width, (int)e.Height);
            lockSurfaceProxy.AckConfigure(e.Serial);
        };
        host.PumpUntil(() => size.W != 0);
        var buffer = locker.CreateBuffer(size.W, size.H, Fill.Solid(size.W, size.H, 0xFF335577));
        surface.Attach(buffer.Proxy, 0, 0);
        surface.Damage(0, 0, size.W, size.H);
        surface.Commit();
        host.PumpUntil(() => lockScene is not null && host.Seat.Keyboard.Focus is not null);
        Assert.Same(layers.Lock, lockScene!.Tree.Parent);

        lockProxy.UnlockAndDestroy();
        host.PumpUntil(() => layers.Windows.Enabled);
        host.DisconnectClient(locker);
    }

    [Fact]
    public void A_second_layer_driver_is_a_second_claim()
    {
        using var host = new CompositorTestHost();
        var layers = new SceneLayers(host.Scene.Root);
        using var services = Registry(host);
        _ = new LayerShellSceneDriver(services, host.Layout, layers);

        Assert.Throws<InvalidOperationException>(() => new LayerShellSceneDriver(services, host.Layout, layers));
    }

    private static BasinServices Registry(CompositorTestHost host) =>
        new BasinServices(host.Display, host.Loop)
            .Use(host.Layout)
            .Use(host.Compositor)
            .Use(host.Buffers)
            .Use(host.Seat);

    private static IProtocolModule ModuleFor(string wireInterface) => wireInterface switch
    {
        "zwlr_layer_shell_v1" => new LayerShellModule(),
        "ext_session_lock_manager_v1" => new SessionLockModule(),
        _ => throw new ArgumentOutOfRangeException(nameof(wireInterface), wireInterface, "unknown module"),
    };
}
