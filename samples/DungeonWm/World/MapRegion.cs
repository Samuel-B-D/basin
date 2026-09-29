namespace DungeonWm;

internal sealed class MapRegion
{
    public MapRegion(int id, RegionKind kind)
    {
        Id = id;
        Kind = kind;
    }

    public int Id { get; }

    public RegionKind Kind { get; }

    public List<GridCell> Cells { get; } = [];

    public int X0 { get; set; }

    public int Z0 { get; set; }

    public int Width { get; set; }

    public int Depth { get; set; }

    public GridSide Entrance { get; set; } = GridSide.North;

    public int Seed { get; set; }

    public int Bends { get; set; }

    public int Workspace { get; set; } = -1;

    public bool Removed { get; set; }

    public void FillRectangle()
    {
        Cells.Clear();
        for (var z = Z0; z < Z0 + Depth; z++)
        {
            for (var x = X0; x < X0 + Width; x++)
            {
                Cells.Add(new GridCell(x, z));
            }
        }
    }
}
