using System.Numerics;

namespace Basin.Scene3D;

public readonly record struct WorldHit(
    WorldHitKind Kind,
    WorldWindow? Window,
    int Layer,
    Surface? Surface,
    double SurfaceX,
    double SurfaceY,
    Vector3 Point,
    Vector3 Normal,
    float Distance,
    WorldFramePart Part = WorldFramePart.None)
{
    public static WorldHit Miss => new(WorldHitKind.None, null, -1, null, 0, 0, Vector3.Zero, Vector3.Zero, float.MaxValue);
}
