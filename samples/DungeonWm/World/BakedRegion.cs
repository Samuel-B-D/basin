using Basin.Scene3D;

namespace DungeonWm;

internal sealed record BakedRegion(
    WorldMesh Mesh,
    List<WorldLight> Lights,
    List<WorldBounds> Blockers,
    List<PropPlacement> DynamicProps,
    WorldBounds Interior,
    RoofKind Roof);
