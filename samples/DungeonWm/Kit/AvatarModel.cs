using Basin.Scene3D;

namespace DungeonWm;

internal sealed record AvatarModel(
    Skeleton Skeleton,
    SkinnedMesh Mesh,
    IReadOnlyList<WorldAvatarAttachment> Attachments,
    IReadOnlyDictionary<string, SkeletonClip> Clips);
