namespace Basin.Scene3D;

public sealed class WorldAvatarAttachment
{
    public WorldAvatarAttachment(string name, int node, WorldMesh mesh)
    {
        Name = name;
        Node = node;
        Mesh = mesh;
    }

    public string Name { get; }

    public int Node { get; }

    public WorldMesh Mesh { get; }

    public bool Visible { get; set; } = true;
}
