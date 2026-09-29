using System.Numerics;
using Basin.Scene3D;

namespace DungeonWm;

internal sealed record DungeonWmFrameStyle(string Name, Vector4 Swatch, WorldMaterial Material)
{
    public static readonly WorldMaterial Brass = new(new Vector4(0.8f, 0.62f, 0.3f, 1), 0.8f, 44, Vector3.Zero);

    public static DungeonWmFrameStyle? Named(string name) => name switch
    {
        "oak" => new(name, Cell(4, 0), new WorldMaterial(new Vector4(1, 0.92f, 0.84f, 1), 0.18f, 18, Vector3.Zero)),
        "gilt" => new(name, Cell(5, 2), new WorldMaterial(new Vector4(1, 0.88f, 0.6f, 1), 0.95f, 56, Vector3.Zero)),
        "iron" => new(name, Cell(3, 0), new WorldMaterial(new Vector4(1.3f, 1.3f, 1.35f, 1), 0.55f, 36, Vector3.Zero)),
        "stone" => new(name, Cell(5, 0), new WorldMaterial(Vector4.One, 0.05f, 8, Vector3.Zero)),
        _ => null,
    };

    private static Vector4 Cell(int column, int row) =>
        new((column + 0.35f) / 8f, (row / 4f) + 0.02f, 0, 0.21f);
}
