using Basin.Avalonia;
using Basin.Hosted;

namespace Basin.Tests;

internal static class HostedDrivers
{
    public static void Without(BasinCompositorHost host, BasinServices services) => services
        .Without("zwlr_layer_shell_v1")
        .Without("ext_session_lock_manager_v1");

    public static void ToplevelLayers(BasinCompositorHost host, BasinServices services)
    {
        _ = new ToplevelLayerShellDriver(services);
        services.Without("ext_session_lock_manager_v1");
    }
}
