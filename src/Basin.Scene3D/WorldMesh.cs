namespace Basin.Scene3D;

public sealed class WorldMesh
{
    public WorldMesh(WorldVertex[] vertices, uint[] indices, WorldImage? image)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentNullException.ThrowIfNull(indices);
        if (indices.Length % 3 != 0)
        {
            throw new ArgumentException("Indices must form whole triangles.", nameof(indices));
        }

        Vertices = vertices;
        Indices = indices;
        Image = image;
        var bounds = WorldBounds.Empty;
        foreach (var vertex in vertices)
        {
            bounds = bounds.Include(vertex.Position);
        }

        Bounds = bounds;
    }

    public WorldVertex[] Vertices { get; }

    public uint[] Indices { get; }

    public WorldImage? Image { get; }

    public WorldBounds Bounds { get; }

    public int TriangleCount => Indices.Length / 3;
}
