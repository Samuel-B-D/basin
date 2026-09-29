using System.Numerics;

namespace Basin.Scene3D;

public sealed class World
{
    public List<WorldRoom> Rooms { get; } = [];

    public List<WorldWindow> Windows { get; } = [];

    public List<WorldPortal> Portals { get; } = [];

    public List<WorldProp> Props { get; } = [];

    public WorldAvatar? Avatar { get; set; }

    public Vector3 ClearColor { get; set; } = new(0.02f, 0.02f, 0.03f);

    public Vector3 Ambient { get; set; } = new(0.18f, 0.17f, 0.2f);

    public WorldRoom? RoomAt(Vector3 point)
    {
        for (var i = 0; i < Rooms.Count; i++)
        {
            if (Rooms[i].Contains(point))
            {
                return Rooms[i];
            }
        }

        return null;
    }

    public WorldRoom? NearestRoom(Vector3 point)
    {
        WorldRoom? best = null;
        var bestDistance = float.MaxValue;
        for (var i = 0; i < Rooms.Count; i++)
        {
            var bounds = Rooms[i].Bounds;
            var clamped = Vector3.Clamp(point, bounds.Min, bounds.Max);
            var distance = Vector3.DistanceSquared(clamped, point);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = Rooms[i];
            }
        }

        return best;
    }
}
