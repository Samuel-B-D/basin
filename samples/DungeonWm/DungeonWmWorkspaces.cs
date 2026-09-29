using Basin;
using Basin.Capabilities;

namespace DungeonWm;

internal sealed class DungeonWmWorkspaces : IWorkspaceModel
{
    private readonly WorkspaceObservers _observers = new();
    private readonly DungeonWmWorld _world;

    public DungeonWmWorkspaces(DungeonWmWorld world)
    {
        _world = world;
    }

    public MapRegion? Active { get; private set; }

    public Func<IEnumerable<DungeonWmWindow>> Windows { get; set; } = () => [];

    public Action<MapRegion>? ActivateRequested { get; set; }

    public Action? CreateRequested { get; set; }

    public Action<MapRegion>? RemoveRequested { get; set; }

    public Action<DungeonWmWindow, MapRegion>? AssignRequested { get; set; }

    public Func<Span<IOutput>, int>? Outputs { get; set; }

    public static ulong IdOf(MapRegion room) => (ulong)room.Workspace + 1;

    public MapRegion? RoomOf(ulong id)
    {
        foreach (var room in _world.Map.Rooms)
        {
            if (!room.Removed && IdOf(room) == id)
            {
                return room;
            }
        }

        return null;
    }

    public void SetActive(MapRegion? room)
    {
        if (ReferenceEquals(room, Active))
        {
            return;
        }

        Active = room;
        _observers.Changed();
    }

    public void RaiseChanged() => _observers.Changed();

    public void RaiseMembersChanged() => _observers.MembersChanged();

    public int EnumerateGroups(Span<WorkspaceGroupInfo> groups)
    {
        if (groups.Length == 0)
        {
            return 0;
        }

        groups[0] = new WorkspaceGroupInfo(1, ClientsCanCreateWorkspaces: true);
        return 1;
    }

    public int EnumerateWorkspaces(ulong groupId, Span<WorkspaceInfo> workspaces)
    {
        if (groupId != 1)
        {
            return 0;
        }

        var count = 0;
        foreach (var room in _world.Map.Rooms)
        {
            if (room.Removed)
            {
                continue;
            }

            if (count >= workspaces.Length)
            {
                return -1;
            }

            workspaces[count++] = new WorkspaceInfo(
                IdOf(room),
                $"room {room.Workspace + 1}",
                $"dungeon-wm-room-{room.Workspace + 1}",
                ReferenceEquals(room, Active) ? WorkspaceStateFlags.Active : WorkspaceStateFlags.None,
                [(uint)room.Workspace]);
        }

        return count;
    }

    public int EnumerateGroupOutputs(ulong groupId, Span<IOutput> outputs) =>
        groupId == 1 && Outputs is { } lookup ? lookup(outputs) : 0;

    public int EnumerateMembers(ulong workspaceId, Span<WorkspaceMember> members)
    {
        var count = 0;
        foreach (var window in Windows())
        {
            if (window.Room is not { } room || IdOf(room) != workspaceId || window.ToplevelId == 0)
            {
                continue;
            }

            if (count >= members.Length)
            {
                return -1;
            }

            members[count++] = new WorkspaceMember(
                window.ToplevelId, new Box(0, 0, window.World.SurfaceWidth, window.World.SurfaceHeight));
        }

        return count;
    }

    public bool Request(ulong targetId, in WorkspaceRequest request)
    {
        switch (request.Kind)
        {
            case WorkspaceRequestKind.Activate when RoomOf(targetId) is { } room:
                ActivateRequested?.Invoke(room);
                return true;
            case WorkspaceRequestKind.Create:
                CreateRequested?.Invoke();
                return true;
            case WorkspaceRequestKind.Remove when RoomOf(targetId) is { } room && _world.Map.Rooms.Count(r => !r.Removed) > 1:
                RemoveRequested?.Invoke(room);
                return true;
            case WorkspaceRequestKind.Assign when RoomOf(targetId) is { } room:
                foreach (var window in Windows())
                {
                    if (window.ToplevelId == request.ToplevelId)
                    {
                        AssignRequested?.Invoke(window, room);
                        return true;
                    }
                }

                return false;
            default:
                return false;
        }
    }

    public void AddObserver(IWorkspaceObserver observer) => _observers.Add(observer);

    public void RemoveObserver(IWorkspaceObserver observer) => _observers.Remove(observer);
}
