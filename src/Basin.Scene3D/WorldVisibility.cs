namespace Basin.Scene3D;

public static class WorldVisibility
{
    public static void Collect(World world, in WorldView view, int roomDepth, WorldVisibleSet into)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(into);
        var frustum = view.Frustum();
        var eye = view.Eye;
        var start = world.RoomAt(eye) ?? world.NearestRoom(eye);
        into.EyeRoom ??= start;
        into.Queue.Clear();
        if (start is not null)
        {
            Visit(start, 0);
        }

        for (var head = 0; head < into.Queue.Count; head++)
        {
            var (room, depth) = into.Queue[head];
            if (depth >= roomDepth)
            {
                continue;
            }

            var doorways = room.Doorways;
            for (var i = 0; i < doorways.Count; i++)
            {
                var doorway = doorways[i];
                var next = ReferenceEquals(doorway.From, room) ? doorway.To : doorway.From;
                if (frustum.Intersects(doorway.Bounds) && frustum.Intersects(next.Bounds))
                {
                    Visit(next, depth + 1);
                }
            }
        }

        var windows = world.Windows;
        for (var i = 0; i < windows.Count; i++)
        {
            var window = windows[i];
            if (!window.Visible || into.ContainsWindow(window) ||
                (window.Room is { } home && !into.ContainsRoom(home)) ||
                !frustum.Intersects(window.Bounds))
            {
                continue;
            }

            into.Windows.Add(window);
            var layers = window.Layers;
            for (var l = 0; l < layers.Length; l++)
            {
                if (layers[l].Surface is { } surface)
                {
                    into.AddSurface(surface);
                }
            }
        }

        var props = world.Props;
        for (var i = 0; i < props.Count; i++)
        {
            var prop = props[i];
            if ((prop.Room is null || into.ContainsRoom(prop.Room)) && !into.Props.Contains(prop) &&
                frustum.Intersects(prop.Bounds))
            {
                into.Props.Add(prop);
            }
        }

        return;

        void Visit(WorldRoom room, int depth)
        {
            if (into.ContainsRoom(room))
            {
                return;
            }

            into.Rooms.Add(room);
            into.Queue.Add((room, depth));
            var portals = room.Portals;
            for (var i = 0; i < portals.Count; i++)
            {
                if (portals[i].Target is not null && frustum.Intersects(portals[i].Bounds) && !into.Portals.Contains(portals[i]))
                {
                    into.Portals.Add(portals[i]);
                }
            }
        }
    }
}
