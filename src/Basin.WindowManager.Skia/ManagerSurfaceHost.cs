using Basin.WindowManager.Skia.Protocol;
using Wayland;

namespace Basin.WindowManager.Skia;

public sealed class ManagerSurfaceHost
{
    private ManagerSurfaceHost(RiverWindowManager wm, WlCompositor compositor, WlShm shm)
    {
        Manager = wm;
        Compositor = compositor;
        Shm = shm;
        Scales = new OutputScales(wm);
        var registry = wm.Display.GetRegistry();
        registry.Global += (_, e) =>
        {
            if (e.Interface == "zwlr_layer_shell_v1")
            {
                LayerShell = registry.Bind<ZwlrLayerShellV1>(e.Name, Math.Min(e.Version, 4));
            }
        };
    }

    public RiverWindowManager Manager { get; }

    public WlCompositor Compositor { get; }

    public WlShm Shm { get; }

    public OutputScales Scales { get; }

    public ZwlrLayerShellV1? LayerShell { get; private set; }

    public static ManagerSurfaceHost? TryCreate(RiverWindowManager wm)
    {
        ArgumentNullException.ThrowIfNull(wm);
        return wm is { Compositor: { } compositor, Shm: { } shm } ? new ManagerSurfaceHost(wm, compositor, shm) : null;
    }

    public WlOutput? ProxyFor(WmOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return Scales.ProxyForName(output.WlOutputName);
    }

    public ManagerSurface Create(WlOutput output, ZwlrLayerShellV1.Layer layer, string scope)
    {
        ArgumentNullException.ThrowIfNull(output);
        var shell = LayerShell ?? throw new InvalidOperationException("the compositor has not offered zwlr_layer_shell_v1");
        return new ManagerSurface(Compositor, Shm, shell, output, layer, scope);
    }
}
