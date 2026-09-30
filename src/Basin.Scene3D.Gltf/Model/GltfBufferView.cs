namespace Basin.Scene3D.Gltf;

public sealed class GltfBufferView
{
    public int Buffer { get; set; }

    public int ByteOffset { get; set; }

    public int ByteLength { get; set; }

    public int? ByteStride { get; set; }
}
