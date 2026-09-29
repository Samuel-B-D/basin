namespace DungeonWm;

internal readonly record struct MapEdge(GridCell Cell, GridSide Side)
{
    public MapEdge Mirror => new(Cell.Step(Side), GridSides.Opposite(Side));

    public MapEdge Canonical => Side is GridSide.North or GridSide.West ? this : Mirror;
}
