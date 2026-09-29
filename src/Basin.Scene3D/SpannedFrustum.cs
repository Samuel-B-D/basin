using System.Numerics;

namespace Basin.Scene3D;

public static class SpannedFrustum
{
    public static (float Left, float Right, float Bottom, float Top) Edges(
        in Box union, in Box slice, float fieldOfView, float near)
    {
        if (union.Width <= 0 || union.Height <= 0)
        {
            throw new ArgumentException("The layout union is empty.", nameof(union));
        }

        var halfHeight = near * MathF.Tan(fieldOfView * 0.5f);
        var halfWidth = halfHeight * union.Width / union.Height;
        var left = PlaneX(slice.X, union, halfWidth);
        var right = PlaneX(slice.X + slice.Width, union, halfWidth);
        var top = PlaneY(slice.Y, union, halfHeight);
        var bottom = PlaneY(slice.Y + slice.Height, union, halfHeight);
        return (left, right, bottom, top);
    }

    public static Matrix4x4 Projection(in Box union, in Box slice, float fieldOfView, float near, float far)
    {
        var (left, right, bottom, top) = Edges(union, slice, fieldOfView, near);
        return Matrix4x4.CreatePerspectiveOffCenter(left, right, bottom, top, near, far);
    }

    public static Vector3 CameraDirection(in Box union, double layoutX, double layoutY, float fieldOfView)
    {
        var halfHeight = MathF.Tan(fieldOfView * 0.5f);
        var halfWidth = halfHeight * union.Width / union.Height;
        var x = (float)(((layoutX - union.X) / union.Width * 2) - 1) * halfWidth;
        var y = (float)(1 - ((layoutY - union.Y) / union.Height * 2)) * halfHeight;
        return Vector3.Normalize(new Vector3(x, y, -1));
    }

    private static float PlaneX(double x, in Box union, float halfWidth) =>
        (float)(((x - union.X) / union.Width * 2) - 1) * halfWidth;

    private static float PlaneY(double y, in Box union, float halfHeight) =>
        (float)(1 - ((y - union.Y) / union.Height * 2)) * halfHeight;
}
