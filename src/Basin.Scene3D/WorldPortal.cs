using System.Numerics;

namespace Basin.Scene3D;

public sealed class WorldPortal
{
    private static readonly Matrix4x4 HalfTurn = Matrix4x4.CreateRotationY(MathF.PI);

    public WorldPortal(WorldRoom room, Vector3 center, float yaw, float width, float height)
    {
        Room = room;
        Center = center;
        Yaw = yaw;
        Width = width;
        Height = height;
    }

    public WorldRoom Room { get; }

    public Vector3 Center { get; }

    public float Yaw { get; }

    public float Width { get; }

    public float Height { get; }

    public WorldPortal? Target { get; set; }

    public Matrix4x4 Frame => Matrix4x4.CreateRotationY(Yaw) * Matrix4x4.CreateTranslation(Center);

    public Vector3 Normal => Vector3.TransformNormal(Vector3.UnitZ, Frame);

    public Vector4 Plane
    {
        get
        {
            var normal = Normal;
            return new Vector4(normal, -Vector3.Dot(normal, Center));
        }
    }

    public Matrix4x4 TransferTo(WorldPortal target)
    {
        Matrix4x4.Invert(Frame, out var inverse);
        return inverse * HalfTurn * target.Frame;
    }

    public void Corners(Span<Vector3> corners)
    {
        var frame = Frame;
        var halfWidth = Width * 0.5f;
        var halfHeight = Height * 0.5f;
        corners[0] = Vector3.Transform(new Vector3(-halfWidth, -halfHeight, 0), frame);
        corners[1] = Vector3.Transform(new Vector3(halfWidth, -halfHeight, 0), frame);
        corners[2] = Vector3.Transform(new Vector3(halfWidth, halfHeight, 0), frame);
        corners[3] = Vector3.Transform(new Vector3(-halfWidth, halfHeight, 0), frame);
    }

    public WorldBounds Bounds
    {
        get
        {
            Span<Vector3> corners = stackalloc Vector3[4];
            Corners(corners);
            var bounds = WorldBounds.Empty;
            foreach (var corner in corners)
            {
                bounds = bounds.Include(corner);
            }

            return bounds;
        }
    }
}
