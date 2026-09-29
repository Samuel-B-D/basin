namespace DungeonWm;

internal sealed class GltfRoot
{
    public GltfScene[]? Scenes { get; set; }

    public int? Scene { get; set; }

    public GltfNode[]? Nodes { get; set; }

    public GltfMesh[]? Meshes { get; set; }

    public GltfAccessor[]? Accessors { get; set; }

    public GltfBufferView[]? BufferViews { get; set; }

    public GltfSkin[]? Skins { get; set; }

    public GltfAnimation[]? Animations { get; set; }

    public GltfImage[]? Images { get; set; }

    public string[]? ExtensionsRequired { get; set; }
}
