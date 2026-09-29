namespace DungeonWm;

internal sealed class MapWall
{
    public MapWall(MapRegion region, MapEdge edge, WallKind kind, string piece)
    {
        Region = region;
        Edge = edge;
        Kind = kind;
        Piece = piece;
    }

    public MapRegion Region { get; }

    public MapEdge Edge { get; }

    public WallKind Kind { get; set; }

    public string Piece { get; set; }

    public bool Torch { get; set; }
}
