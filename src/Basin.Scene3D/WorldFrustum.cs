using System.Numerics;

namespace Basin.Scene3D;

public readonly struct WorldFrustum
{
    private readonly Vector4 _left;
    private readonly Vector4 _right;
    private readonly Vector4 _bottom;
    private readonly Vector4 _top;
    private readonly Vector4 _near;
    private readonly Vector4 _far;

    private WorldFrustum(Vector4 left, Vector4 right, Vector4 bottom, Vector4 top, Vector4 near, Vector4 far)
    {
        _left = left;
        _right = right;
        _bottom = bottom;
        _top = top;
        _near = near;
        _far = far;
    }

    public static WorldFrustum From(in Matrix4x4 m)
    {
        var c1 = new Vector4(m.M11, m.M21, m.M31, m.M41);
        var c2 = new Vector4(m.M12, m.M22, m.M32, m.M42);
        var c3 = new Vector4(m.M13, m.M23, m.M33, m.M43);
        var c4 = new Vector4(m.M14, m.M24, m.M34, m.M44);
        return new WorldFrustum(c4 + c1, c4 - c1, c4 + c2, c4 - c2, c3, c4 - c3);
    }

    public bool Intersects(in WorldBounds box)
    {
        if (box.IsEmpty)
        {
            return false;
        }

        return Inside(_left, box) && Inside(_right, box) && Inside(_bottom, box) &&
            Inside(_top, box) && Inside(_near, box) && Inside(_far, box);
    }

    private static bool Inside(Vector4 plane, in WorldBounds box)
    {
        var x = plane.X >= 0 ? box.Max.X : box.Min.X;
        var y = plane.Y >= 0 ? box.Max.Y : box.Min.Y;
        var z = plane.Z >= 0 ? box.Max.Z : box.Min.Z;
        return (plane.X * x) + (plane.Y * y) + (plane.Z * z) + plane.W >= 0;
    }
}
