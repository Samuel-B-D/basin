namespace DungeonWm;

internal sealed record KitPiece(string Name, KitRole Role, KitShape Shape, params string[] Hidden)
{
    public string Resource => $"KayKit/Dungeon/{Name}.glb";
}
