using System.Numerics;

namespace Basin.Scene3D;

public readonly struct WorldView
{
    private WorldView(
        in Matrix4x4 cameraToWorld, in Matrix4x4 worldToCamera, in Matrix4x4 projection,
        in Box union, in Box slice, float fieldOfView, float near, float far)
    {
        CameraToWorld = cameraToWorld;
        WorldToCamera = worldToCamera;
        Projection = projection;
        ViewProjection = worldToCamera * projection;
        Union = union;
        Slice = slice;
        FieldOfView = fieldOfView;
        Near = near;
        Far = far;
    }

    public Matrix4x4 CameraToWorld { get; }

    public Matrix4x4 WorldToCamera { get; }

    public Matrix4x4 Projection { get; }

    public Matrix4x4 ViewProjection { get; }

    public Box Union { get; }

    public Box Slice { get; }

    public float FieldOfView { get; }

    public float Near { get; }

    public float Far { get; }

    public Vector3 Eye => CameraToWorld.Translation;

    public static WorldView Create(
        in Matrix4x4 cameraToWorld, in Box union, in Box slice, float fieldOfView, float near, float far)
    {
        if (!Matrix4x4.Invert(cameraToWorld, out var worldToCamera))
        {
            throw new ArgumentException("The camera pose is not invertible.", nameof(cameraToWorld));
        }

        var projection = SpannedFrustum.Projection(union, slice, fieldOfView, near, far);
        return new WorldView(cameraToWorld, worldToCamera, projection, union, slice, fieldOfView, near, far);
    }

    public WorldView Through(in Matrix4x4 transfer, in Vector4 clipPlane)
    {
        var cameraToWorld = CameraToWorld * transfer;
        Matrix4x4.Invert(cameraToWorld, out var worldToCamera);
        var cameraPlane = Vector4.Transform(clipPlane, Matrix4x4.Transpose(cameraToWorld));
        var projection = Oblique(Projection, cameraPlane);
        return new WorldView(cameraToWorld, worldToCamera, projection, Union, Slice, FieldOfView, Near, Far);
    }

    public WorldRay RayAt(double layoutX, double layoutY)
    {
        var direction = SpannedFrustum.CameraDirection(Union, layoutX, layoutY, FieldOfView);
        return new WorldRay(Eye, Vector3.Normalize(Vector3.TransformNormal(direction, CameraToWorld)));
    }

    public bool TryProject(Vector3 world, out double layoutX, out double layoutY, out float depth)
    {
        var clip = Vector4.Transform(new Vector4(world, 1), ViewProjection);
        if (clip.W <= 1e-6f)
        {
            layoutX = layoutY = 0;
            depth = 0;
            return false;
        }

        var x = clip.X / clip.W;
        var y = clip.Y / clip.W;
        depth = clip.Z / clip.W;
        layoutX = Slice.X + ((x + 1) * 0.5 * Slice.Width);
        layoutY = Slice.Y + ((1 - y) * 0.5 * Slice.Height);
        return true;
    }

    public WorldFrustum Frustum() => WorldFrustum.From(ViewProjection);

    private static Matrix4x4 Oblique(in Matrix4x4 projection, Vector4 plane)
    {
        if (plane.W > 0)
        {
            plane = -plane;
        }

        var q = new Vector4(
            (MathF.Sign(plane.X) + projection.M31) / projection.M11,
            (MathF.Sign(plane.Y) + projection.M32) / projection.M22,
            -1,
            (1 + projection.M33) / projection.M43);
        var scale = 1f / Vector4.Dot(plane, q);
        var c = plane * scale;
        var result = projection;
        result.M13 = c.X;
        result.M23 = c.Y;
        result.M33 = c.Z;
        result.M43 = c.W;
        return result;
    }
}
