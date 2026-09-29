namespace DungeonWm;

internal readonly record struct GridCell(int X, int Z)
{
    public GridCell Step(GridSide side) => side switch
    {
        GridSide.North => new GridCell(X, Z - 1),
        GridSide.South => new GridCell(X, Z + 1),
        GridSide.East => new GridCell(X + 1, Z),
        _ => new GridCell(X - 1, Z),
    };
}
