namespace Basin.Scene3D;

public sealed class WorldRoom
{
    private WorldMesh? _structure;

    public WorldRoom(int id, WorldRoomKind kind, WorldBounds bounds)
    {
        Id = id;
        Kind = kind;
        Bounds = bounds;
    }

    public int Id { get; }

    public WorldRoomKind Kind { get; }

    public WorldBounds Bounds { get; set; }

    public int Version { get; private set; }

    public WorldMesh? Structure
    {
        get => _structure;
        set
        {
            _structure = value;
            Version++;
        }
    }

    public List<WorldLight> Lights { get; } = [];

    public List<WorldDoorway> Doorways { get; } = [];

    public List<WorldBounds> Blockers { get; } = [];

    public List<WorldWindow> Windows { get; } = [];

    public List<WorldPortal> Portals { get; } = [];

    public object? Tag { get; set; }

    public bool Contains(System.Numerics.Vector3 point) => Bounds.Contains(point);
}
