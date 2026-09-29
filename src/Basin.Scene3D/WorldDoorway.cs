using System.Numerics;

namespace Basin.Scene3D;

public sealed class WorldDoorway
{
    public WorldDoorway(WorldRoom from, WorldRoom to, Vector3 center, Vector3 normal, float width, float height)
    {
        From = from;
        To = to;
        Center = center;
        Normal = Vector3.Normalize(normal);
        Width = width;
        Height = height;
    }

    public WorldRoom From { get; }

    public WorldRoom To { get; }

    public Vector3 Center { get; }

    public Vector3 Normal { get; }

    public float Width { get; }

    public float Height { get; }

    public WorldBounds Bounds
    {
        get
        {
            var side = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, Normal)) * (Width * 0.5f);
            var up = Vector3.UnitY * (Height * 0.5f);
            return WorldBounds.Empty
                .Include(Center - side - up)
                .Include(Center + side + up)
                .Include(Center - side + up)
                .Include(Center + side - up);
        }
    }
}
