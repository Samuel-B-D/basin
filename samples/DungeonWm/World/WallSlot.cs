using System.Numerics;

namespace DungeonWm;

internal readonly record struct WallSlot(MapRegion Room, MapEdge Edge)
{
    public GridSide Side => Edge.Side;

    public int Along => Side is GridSide.North or GridSide.South ? Edge.Cell.X : Edge.Cell.Z;

    public Vector3 Center(float unit) => GridSides.WallCenter(Edge.Cell, Edge.Side) * unit;

    public float Yaw => GridSides.InwardYaw(Side);

    public Vector3 Inward => GridSides.Inward(Side);
}
