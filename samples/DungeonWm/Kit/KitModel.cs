using Basin.Scene3D;

namespace DungeonWm;

internal sealed record KitModel(KitPiece Piece, WorldMesh Mesh)
{
    public WorldBounds Bounds => Mesh.Bounds;
}
