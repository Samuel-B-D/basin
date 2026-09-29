namespace Basin.Scene3D;

public sealed class SkinnedMesh
{
    public SkinnedMesh(SkinnedVertex[] vertices, uint[] indices, WorldImage? image)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentNullException.ThrowIfNull(indices);
        Vertices = vertices;
        Indices = indices;
        Image = image;
    }

    public SkinnedVertex[] Vertices { get; }

    public uint[] Indices { get; }

    public WorldImage? Image { get; }

    public int TriangleCount => Indices.Length / 3;
}
