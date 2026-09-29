namespace Basin.Scene3D;

public sealed class WorldVisibleSet
{
    internal readonly List<(WorldRoom Room, int Depth)> Queue = [];

    public List<WorldRoom> Rooms { get; } = [];

    public List<WorldWindow> Windows { get; } = [];

    public List<WorldProp> Props { get; } = [];

    public List<WorldPortal> Portals { get; } = [];

    public List<Surface> Surfaces { get; } = [];

    public WorldRoom? EyeRoom { get; internal set; }

    public void Clear()
    {
        Rooms.Clear();
        Windows.Clear();
        Props.Clear();
        Portals.Clear();
        Surfaces.Clear();
        Queue.Clear();
        EyeRoom = null;
    }

    public bool ContainsSurface(Surface surface)
    {
        for (var i = 0; i < Surfaces.Count; i++)
        {
            if (ReferenceEquals(Surfaces[i], surface))
            {
                return true;
            }
        }

        return false;
    }

    public bool ContainsWindow(WorldWindow window)
    {
        for (var i = 0; i < Windows.Count; i++)
        {
            if (ReferenceEquals(Windows[i], window))
            {
                return true;
            }
        }

        return false;
    }

    public bool ContainsRoom(WorldRoom room)
    {
        for (var i = 0; i < Rooms.Count; i++)
        {
            if (ReferenceEquals(Rooms[i], room))
            {
                return true;
            }
        }

        return false;
    }

    internal void AddSurface(Surface surface)
    {
        if (!ContainsSurface(surface))
        {
            Surfaces.Add(surface);
        }
    }
}
