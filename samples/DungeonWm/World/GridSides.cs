using System.Numerics;

namespace DungeonWm;

internal static class GridSides
{
    public static readonly GridSide[] All = [GridSide.North, GridSide.East, GridSide.South, GridSide.West];

    public static GridSide Opposite(GridSide side) => (GridSide)(((int)side + 2) % 4);

    public static GridSide Left(GridSide side) => (GridSide)(((int)side + 3) % 4);

    public static GridSide Right(GridSide side) => (GridSide)(((int)side + 1) % 4);

    public static float InwardYaw(GridSide side) => side switch
    {
        GridSide.North => 0,
        GridSide.South => MathF.PI,
        GridSide.West => MathF.PI / 2,
        _ => -MathF.PI / 2,
    };

    public static Vector3 Inward(GridSide side) => side switch
    {
        GridSide.North => Vector3.UnitZ,
        GridSide.South => -Vector3.UnitZ,
        GridSide.West => Vector3.UnitX,
        _ => -Vector3.UnitX,
    };

    public static Vector3 WallCenter(GridCell cell, GridSide side) => side switch
    {
        GridSide.North => new Vector3((4 * cell.X) + 2, 0, 4 * cell.Z),
        GridSide.South => new Vector3((4 * cell.X) + 2, 0, (4 * cell.Z) + 4),
        GridSide.West => new Vector3(4 * cell.X, 0, (4 * cell.Z) + 2),
        _ => new Vector3((4 * cell.X) + 4, 0, (4 * cell.Z) + 2),
    };

    public static Vector3 CellCenter(GridCell cell) => new((4 * cell.X) + 2, 0, (4 * cell.Z) + 2);
}
