namespace Basin.Scene3D;

public readonly struct WorldFrame
{
    public WorldFrame(World world, in WorldView view, FrameTick tick, WorldVisibleSet visible, WorldRenderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(visible);
        ArgumentNullException.ThrowIfNull(settings);
        World = world;
        View = view;
        Tick = tick;
        Visible = visible;
        Settings = settings;
    }

    public World World { get; }

    public WorldView View { get; }

    public FrameTick Tick { get; }

    public WorldVisibleSet Visible { get; }

    public WorldRenderSettings Settings { get; }
}
