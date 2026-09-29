using System.Numerics;

namespace Basin.Physics;

public readonly record struct PhysicsPose3D(Vector3 Position, Quaternion Orientation)
{
    public static PhysicsPose3D At(Vector3 position) => new(position, Quaternion.Identity);

    public Matrix4x4 Matrix => Matrix4x4.CreateFromQuaternion(Orientation) * Matrix4x4.CreateTranslation(Position);

    public Vector3 ToWorld(Vector3 local) => Position + Vector3.Transform(local, Orientation);

    public Vector3 ToLocal(Vector3 world) => Vector3.Transform(world - Position, Quaternion.Conjugate(Orientation));

    public static PhysicsPose3D Lerp(in PhysicsPose3D from, in PhysicsPose3D to, float amount) =>
        new(Vector3.Lerp(from.Position, to.Position, amount), Quaternion.Slerp(from.Orientation, to.Orientation, amount));
}
