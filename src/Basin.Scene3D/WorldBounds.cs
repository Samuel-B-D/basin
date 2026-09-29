using System.Numerics;

namespace Basin.Scene3D;

public readonly record struct WorldBounds(Vector3 Min, Vector3 Max)
{
    public static WorldBounds Empty => new(new Vector3(float.MaxValue), new Vector3(float.MinValue));

    public bool IsEmpty => Min.X > Max.X || Min.Y > Max.Y || Min.Z > Max.Z;

    public Vector3 Size => IsEmpty ? Vector3.Zero : Max - Min;

    public Vector3 Center => (Min + Max) * 0.5f;

    public WorldBounds Include(Vector3 point) => new(Vector3.Min(Min, point), Vector3.Max(Max, point));

    public WorldBounds Union(in WorldBounds other) =>
        other.IsEmpty ? this : IsEmpty ? other : new(Vector3.Min(Min, other.Min), Vector3.Max(Max, other.Max));

    public bool Contains(Vector3 point) =>
        point.X >= Min.X && point.X <= Max.X &&
        point.Y >= Min.Y && point.Y <= Max.Y &&
        point.Z >= Min.Z && point.Z <= Max.Z;

    public bool Intersects(in WorldBounds other) =>
        Min.X <= other.Max.X && Max.X >= other.Min.X &&
        Min.Y <= other.Max.Y && Max.Y >= other.Min.Y &&
        Min.Z <= other.Max.Z && Max.Z >= other.Min.Z;

    public WorldBounds Transformed(in Matrix4x4 matrix)
    {
        if (IsEmpty)
        {
            return this;
        }

        var result = Empty;
        for (var i = 0; i < 8; i++)
        {
            var corner = new Vector3(
                (i & 1) == 0 ? Min.X : Max.X,
                (i & 2) == 0 ? Min.Y : Max.Y,
                (i & 4) == 0 ? Min.Z : Max.Z);
            result = result.Include(Vector3.Transform(corner, matrix));
        }

        return result;
    }
}
