namespace DungeonWm;

internal sealed class GltfAccessor
{
    public int? BufferView { get; set; }

    public int ByteOffset { get; set; }

    public int ComponentType { get; set; }

    public bool Normalized { get; set; }

    public int Count { get; set; }

    public string Type { get; set; } = "SCALAR";
}
