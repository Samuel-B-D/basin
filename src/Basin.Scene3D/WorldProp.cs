using System.Numerics;

namespace Basin.Scene3D;

public sealed class WorldProp
{
    public WorldProp(WorldMesh mesh)
    {
        Mesh = mesh;
    }

    public WorldMesh Mesh { get; }

    public Vector3 Position { get; set; }

    public Quaternion Orientation { get; set; } = Quaternion.Identity;

    public WorldRoom? Room { get; set; }

    public object? Tag { get; set; }

    public Matrix4x4 Model => Matrix4x4.CreateFromQuaternion(Orientation) * Matrix4x4.CreateTranslation(Position);

    public WorldBounds Bounds => Mesh.Bounds.Transformed(Model);
}
