namespace Basin.Scene3D.Gltf;

public sealed class GltfAnimation
{
    public string? Name { get; set; }

    public GltfChannel[]? Channels { get; set; }

    public GltfAnimationSampler[]? Samplers { get; set; }
}
