using System.Numerics;
using Basin.Scene3D;

namespace DungeonWm;

internal static class GlbMeshes
{
    public static WorldMesh Flatten(GlbDocument document, IReadOnlyCollection<string> hidden, WorldImage? image)
    {
        var world = document.WorldMatrices();
        var vertices = new List<WorldVertex>();
        var indices = new List<uint>();
        foreach (var root in document.SceneRoots())
        {
            Walk(root);
        }

        return new WorldMesh([.. vertices], [.. indices], image);

        void Walk(int node)
        {
            var info = document.Nodes[node];
            if (info.Name is { } name && hidden.Contains(name))
            {
                return;
            }

            if (info.Mesh is { } mesh && info.Skin is null)
            {
                Append(document, mesh, world[node], vertices, indices);
            }

            foreach (var child in info.Children ?? [])
            {
                Walk(child);
            }
        }
    }

    public static void Append(
        GlbDocument document, int mesh, in Matrix4x4 transform, List<WorldVertex> vertices, List<uint> indices)
    {
        Matrix4x4.Invert(transform, out var inverse);
        var normalMatrix = Matrix4x4.Transpose(inverse);
        foreach (var primitive in (document.Root.Meshes ?? [])[mesh].Primitives ?? [])
        {
            if ((primitive.Mode ?? 4) != 4 || primitive.Attributes is not { } attributes ||
                !attributes.TryGetValue("POSITION", out var positionAccessor))
            {
                continue;
            }

            var positions = document.ReadFloats(positionAccessor);
            var normals = attributes.TryGetValue("NORMAL", out var n) ? document.ReadFloats(n) : null;
            var uvs = attributes.TryGetValue("TEXCOORD_0", out var t) ? document.ReadFloats(t) : null;
            var count = positions.Length / 3;
            var first = (uint)vertices.Count;
            for (var i = 0; i < count; i++)
            {
                var position = Vector3.Transform(
                    new Vector3(positions[i * 3], positions[(i * 3) + 1], positions[(i * 3) + 2]), transform);
                var normal = normals is null
                    ? Vector3.UnitY
                    : Vector3.Normalize(Vector3.TransformNormal(
                        new Vector3(normals[i * 3], normals[(i * 3) + 1], normals[(i * 3) + 2]), normalMatrix));
                var uv = uvs is null ? Vector2.Zero : new Vector2(uvs[i * 2], uvs[(i * 2) + 1]);
                vertices.Add(new WorldVertex(position, normal, uv));
            }

            if (primitive.Indices is { } indexAccessor)
            {
                foreach (var index in document.ReadIntegers(indexAccessor))
                {
                    indices.Add(first + index);
                }
            }
            else
            {
                for (var i = 0u; i < count; i++)
                {
                    indices.Add(first + i);
                }
            }
        }
    }
}
