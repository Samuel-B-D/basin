namespace Basin.Scene3D.Gltf;

public sealed class GltfNode
{
    public string? Name { get; set; }

    public int? Mesh { get; set; }

    public int? Skin { get; set; }

    public int[]? Children { get; set; }

    public float[]? Translation { get; set; }

    public float[]? Rotation { get; set; }

    public float[]? Scale { get; set; }

    public float[]? Matrix { get; set; }
}
