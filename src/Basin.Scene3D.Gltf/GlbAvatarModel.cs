using Basin.Scene3D;

namespace Basin.Scene3D.Gltf;

public sealed record GlbAvatarModel(
    Skeleton Skeleton,
    SkinnedMesh Mesh,
    IReadOnlyList<WorldAvatarAttachment> Attachments,
    IReadOnlyDictionary<string, SkeletonClip> Clips);
