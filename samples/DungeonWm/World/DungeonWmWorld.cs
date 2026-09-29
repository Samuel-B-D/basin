using System.Numerics;
using Basin.Scene3D;

namespace DungeonWm;

internal sealed class DungeonWmWorld
{
    private readonly Dictionary<MapRegion, WorldRoom> _rooms = [];
    private readonly Dictionary<WorldRoom, MapRegion> _regions = [];
    private readonly Dictionary<(MapRegion Room, GridSide Side, int Along), DungeonWmWindow> _occupied = [];
    private readonly List<DungeonWmProp> _props = [];
    private int _bakedVersion = -1;

    public DungeonWmWorld(Kit kit, DungeonWmSettings settings)
    {
        Kit = kit;
        Settings = settings;
        Map = new DungeonWmMap(settings.Map);
        Baker = new RoomBaker(kit, settings.Bake);
        World.ClearColor = settings.SkyColor;
        World.Ambient = new Vector3(0.3f, 0.28f, 0.3f);
    }

    public Kit Kit { get; }

    public DungeonWmSettings Settings { get; }

    public DungeonWmMap Map { get; }

    public RoomBaker Baker { get; }

    public World World { get; } = new();

    public float Unit => Baker.Unit;

    public IReadOnlyList<DungeonWmProp> Props => _props;

    public event Action? Rebuilt;

    public WorldRoom RoomOf(MapRegion region) => _rooms[region];

    public MapRegion? RegionOf(WorldRoom? room) => room is not null && _regions.TryGetValue(room, out var region) ? region : null;

    public MapRegion AddRoom()
    {
        var room = Map.AddRoom();
        room.Workspace = Map.Rooms.Count - 1;
        Rebuild();
        return room;
    }

    public void Rebuild()
    {
        if (_bakedVersion == Map.Version)
        {
            return;
        }

        _bakedVersion = Map.Version;
        foreach (var region in Map.Regions)
        {
            var baked = Baker.Bake(Map, region);
            if (!_rooms.TryGetValue(region, out var room))
            {
                room = new WorldRoom(region.Id, region.Kind == RegionKind.Room ? WorldRoomKind.Room : WorldRoomKind.Corridor, baked.Interior)
                {
                    Tag = region,
                };
                _rooms[region] = room;
                _regions[room] = region;
                World.Rooms.Add(room);
                foreach (var prop in baked.DynamicProps)
                {
                    var worldProp = new WorldProp(Kit[prop.Piece].Mesh)
                    {
                        Position = prop.Position,
                        Orientation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, prop.Yaw),
                        Room = room,
                    };
                    World.Props.Add(worldProp);
                    _props.Add(new DungeonWmProp(prop.Piece, worldProp));
                }
            }

            room.Bounds = baked.Interior;
            room.Structure = ScaleWorld(baked.Mesh);
            room.Lights.Clear();
            room.Lights.AddRange(baked.Lights);
            room.Blockers.Clear();
            room.Blockers.AddRange(baked.Blockers);
            room.Doorways.Clear();
        }

        foreach (var doorway in Map.Doorways)
        {
            var from = _rooms[doorway.Room];
            var to = _rooms[doorway.Corridor];
            var center = GridSides.WallCenter(doorway.Edge.Cell, doorway.Edge.Side) * Unit;
            center.Y = RoomBaker.DoorHeight * 0.5f * Unit;
            var link = new WorldDoorway(
                from, to, center, GridSides.Inward(doorway.Edge.Side),
                RoomBaker.DoorHalfWidth * 2 * Unit, RoomBaker.DoorHeight * Unit);
            from.Doorways.Add(link);
            to.Doorways.Add(link);
        }

        Rebuilt?.Invoke();
    }

    private static WorldMesh ScaleWorld(WorldMesh mesh) => mesh;

    public IEnumerable<WallSlot> SlotsOf(MapRegion room)
    {
        foreach (var wall in Map.WallsOf(room))
        {
            if (wall.Kind == WallKind.Slot)
            {
                yield return new WallSlot(room, wall.Edge);
            }
        }
    }

    public bool IsFree(WallSlot slot) => !_occupied.ContainsKey((slot.Room, slot.Side, slot.Along));

    public DungeonWmWindow? Occupant(WallSlot slot) => _occupied.GetValueOrDefault((slot.Room, slot.Side, slot.Along));

    public void Occupy(WallSlot slot, DungeonWmWindow window) => _occupied[(slot.Room, slot.Side, slot.Along)] = window;

    public void Release(DungeonWmWindow window)
    {
        foreach (var key in _occupied.Where(pair => ReferenceEquals(pair.Value, window)).Select(pair => pair.Key).ToList())
        {
            _occupied.Remove(key);
        }
    }

    public WallSlot? FindSlot(MapRegion room, (GridSide Side, int Along) key)
    {
        foreach (var slot in SlotsOf(room))
        {
            if (slot.Side == key.Side && slot.Along == key.Along)
            {
                return slot;
            }
        }

        return null;
    }

    public WallSlot? Neighbour(WallSlot slot)
    {
        foreach (var other in SlotsOf(slot.Room))
        {
            if (other.Side == slot.Side && other.Along == slot.Along + 1)
            {
                return other;
            }
        }

        return null;
    }

    public WallSlot? FreeSlotFacing(MapRegion room, Vector3 eye, Vector3 forward, bool wide)
    {
        WallSlot? best = null;
        var bestScore = float.MinValue;
        foreach (var slot in SlotsOf(room))
        {
            if (!IsFree(slot) || (wide && (Neighbour(slot) is not { } next || !IsFree(next))))
            {
                continue;
            }

            var center = slot.Center(Unit);
            var toSlot = center - eye;
            toSlot.Y = 0;
            var distance = toSlot.Length();
            var score = distance > 0.01f ? Vector3.Dot(toSlot / distance, forward) - (distance * 0.02f) : 0;
            if (score > bestScore)
            {
                bestScore = score;
                best = slot;
            }
        }

        return best;
    }

    public MapRegion? RoomAt(Vector3 point)
    {
        foreach (var region in Map.Rooms)
        {
            if (!region.Removed && _rooms.TryGetValue(region, out var room) && room.Contains(point))
            {
                return region;
            }
        }

        return null;
    }

    public Vector3 EntranceOf(MapRegion room, float eyeHeight)
    {
        foreach (var doorway in Map.Doorways)
        {
            if (ReferenceEquals(doorway.Room, room))
            {
                var edge = doorway.Edge;
                var inside = room.Cells.Contains(edge.Cell) ? edge.Cell : edge.Mirror.Cell;
                var center = GridSides.CellCenter(inside) * Unit;
                return center with { Y = eyeHeight };
            }
        }

        var middle = GridSides.CellCenter(new GridCell(room.X0 + (room.Width / 2), room.Z0 + (room.Depth / 2))) * Unit;
        return middle with { Y = eyeHeight };
    }

    public Vector3 CenterOf(MapRegion room, float eyeHeight)
    {
        var center = new Vector3((room.X0 * 4) + (room.Width * 2), 0, (room.Z0 * 4) + (room.Depth * 2)) * Unit;
        return center with { Y = eyeHeight };
    }

    public float EntranceYaw(MapRegion room)
    {
        var center = GridSides.CellCenter(new GridCell(room.X0 + (room.Width / 2), room.Z0 + (room.Depth / 2))) * Unit;
        var entrance = EntranceOf(room, 0);
        var toward = center - entrance;
        return toward.LengthSquared() < 1e-4f ? 0 : MathF.Atan2(-toward.X, -toward.Z);
    }
}
