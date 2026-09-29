using System.Numerics;

namespace Basin.Scene3D;

public sealed class WorldCamera
{
    private const float PitchLimit = 1.55f;
    private float _pitch;

    public Vector3 Position { get; set; }

    public float Yaw { get; set; }

    public float Pitch
    {
        get => _pitch;
        set => _pitch = Math.Clamp(value, -PitchLimit, PitchLimit);
    }

    public float FieldOfView { get; set; } = MathF.PI * 70f / 180f;

    public float Near { get; set; } = 0.05f;

    public float Far { get; set; } = 200f;

    public Quaternion Orientation => Quaternion.CreateFromYawPitchRoll(Yaw, Pitch, 0);

    public Vector3 Forward => Vector3.Transform(-Vector3.UnitZ, Orientation);

    public Vector3 Right => Vector3.Transform(Vector3.UnitX, Orientation);

    public Vector3 Up => Vector3.Transform(Vector3.UnitY, Orientation);

    public Vector3 Flat => new(-MathF.Sin(Yaw), 0, -MathF.Cos(Yaw));

    public Vector3 FlatRight => new(MathF.Cos(Yaw), 0, -MathF.Sin(Yaw));

    public Matrix4x4 CameraToWorld =>
        Matrix4x4.CreateFromQuaternion(Orientation) * Matrix4x4.CreateTranslation(Position);

    public WorldView ViewOf(in Box union, in Box slice) =>
        WorldView.Create(CameraToWorld, union, slice, FieldOfView, Near, Far);
}
