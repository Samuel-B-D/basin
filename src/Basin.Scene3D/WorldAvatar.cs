using System.Numerics;

namespace Basin.Scene3D;

public sealed class WorldAvatar
{
    public WorldAvatar(Skeleton skeleton, SkinnedMesh mesh, IReadOnlyList<WorldAvatarAttachment> attachments)
    {
        Skeleton = skeleton;
        Mesh = mesh;
        Attachments = attachments;
        Pose = new SkeletonPose(skeleton);
    }

    public Skeleton Skeleton { get; }

    public SkinnedMesh Mesh { get; }

    public IReadOnlyList<WorldAvatarAttachment> Attachments { get; }

    public SkeletonPose Pose { get; }

    public Vector3 Position { get; set; }

    public float Yaw { get; set; }

    public float Scale { get; set; } = 1;

    public bool Visible { get; set; } = true;

    public Matrix4x4 Model =>
        Matrix4x4.CreateScale(Scale) * Matrix4x4.CreateRotationY(Yaw) * Matrix4x4.CreateTranslation(Position);
}
