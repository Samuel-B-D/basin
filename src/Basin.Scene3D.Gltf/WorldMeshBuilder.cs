using System.Numerics;
using Basin.Scene3D;

namespace Basin.Scene3D.Gltf;

public sealed class WorldMeshBuilder
{
    private readonly List<WorldVertex> _vertices = [];
    private readonly List<uint> _indices = [];

    public int Triangles => _indices.Count / 3;

    public void Add(WorldMesh mesh, in Matrix4x4 transform)
    {
        Matrix4x4.Invert(transform, out var inverse);
        var normals = Matrix4x4.Transpose(inverse);
        var first = (uint)_vertices.Count;
        foreach (var vertex in mesh.Vertices)
        {
            _vertices.Add(new WorldVertex(
                Vector3.Transform(vertex.Position, transform),
                Vector3.Normalize(Vector3.TransformNormal(vertex.Normal, normals)),
                vertex.Uv));
        }

        foreach (var index in mesh.Indices)
        {
            _indices.Add(first + index);
        }
    }

    public WorldMesh Build(WorldImage? image) => new([.. _vertices], [.. _indices], image);
}
