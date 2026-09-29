using System.Numerics;

namespace Basin.Scene3D;

public readonly record struct WorldRay(Vector3 Origin, Vector3 Direction)
{
    public Vector3 At(float distance) => Origin + (Direction * distance);

    public WorldRay Transformed(in Matrix4x4 matrix) =>
        new(Vector3.Transform(Origin, matrix), Vector3.TransformNormal(Direction, matrix));

    public bool TryPlane(Vector3 point, Vector3 normal, out float distance)
    {
        var facing = Vector3.Dot(Direction, normal);
        if (MathF.Abs(facing) < 1e-7f)
        {
            distance = 0;
            return false;
        }

        distance = Vector3.Dot(point - Origin, normal) / facing;
        return distance >= 0;
    }

    public bool TryBox(in WorldBounds box, out float distance)
    {
        var near = 0f;
        var far = float.MaxValue;
        for (var axis = 0; axis < 3; axis++)
        {
            var origin = axis == 0 ? Origin.X : axis == 1 ? Origin.Y : Origin.Z;
            var direction = axis == 0 ? Direction.X : axis == 1 ? Direction.Y : Direction.Z;
            var min = axis == 0 ? box.Min.X : axis == 1 ? box.Min.Y : box.Min.Z;
            var max = axis == 0 ? box.Max.X : axis == 1 ? box.Max.Y : box.Max.Z;
            if (MathF.Abs(direction) < 1e-9f)
            {
                if (origin < min || origin > max)
                {
                    distance = 0;
                    return false;
                }

                continue;
            }

            var t1 = (min - origin) / direction;
            var t2 = (max - origin) / direction;
            if (t1 > t2)
            {
                (t1, t2) = (t2, t1);
            }

            near = MathF.Max(near, t1);
            far = MathF.Min(far, t2);
            if (near > far)
            {
                distance = 0;
                return false;
            }
        }

        distance = near;
        return true;
    }
}
