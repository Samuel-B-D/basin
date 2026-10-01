using System.Numerics;

namespace DungeonWm;

internal sealed class DoorwayGraph
{
    private DungeonWmMap? _map;
    private float _unit = 1;
    private Vector3[] _centers = [];
    private MapRegion[] _from = [];
    private MapRegion[] _to = [];
    private float[] _weights = [];
    private float[] _distance = [];
    private int[] _previous = [];
    private int[] _route = [];
    private bool[] _done = [];
    private int _count;

    public int Count => _count;

    public Vector3 CenterOf(int doorway) => _centers[doorway];

    public void Build(DungeonWmMap map, float unit)
    {
        _map = map;
        _unit = unit;
        _count = map.Doorways.Count;
        _centers = new Vector3[_count];
        _from = new MapRegion[_count];
        _to = new MapRegion[_count];
        _weights = new float[_count * _count];
        _distance = new float[_count];
        _previous = new int[_count];
        _route = new int[_count];
        _done = new bool[_count];
        for (var i = 0; i < _count; i++)
        {
            var doorway = map.Doorways[i];
            var center = GridSides.WallCenter(doorway.Edge.Cell, doorway.Edge.Side) * unit;
            center.Y = RoomBaker.DoorHeight * 0.5f * unit;
            _centers[i] = center;
            _from[i] = doorway.Room;
            _to[i] = doorway.Corridor;
        }

        for (var i = 0; i < _count; i++)
        {
            for (var j = 0; j < _count; j++)
            {
                _weights[(i * _count) + j] = i != j && SharesRegion(i, j)
                    ? Vector3.Distance(_centers[i], _centers[j])
                    : float.PositiveInfinity;
            }
        }
    }

    public MapRegion? RegionAt(Vector3 point)
    {
        if (_map is null)
        {
            return null;
        }

        var size = 4 * _unit;
        return _map.RegionAt(new GridCell((int)MathF.Floor(point.X / size), (int)MathF.Floor(point.Z / size)));
    }

    public SoundPath Path(Vector3 listener, Vector3 source, MapRegion? sourceHint = null)
    {
        var here = RegionAt(listener);
        var there = RegionAt(source) ?? sourceHint;
        if (here is null || there is null || ReferenceEquals(here, there) || _count == 0)
        {
            return SoundPath.Direct(listener, source);
        }

        for (var i = 0; i < _count; i++)
        {
            _distance[i] = Touches(i, here) ? Vector3.Distance(listener, _centers[i]) : float.PositiveInfinity;
            _previous[i] = -1;
            _done[i] = false;
        }

        while (true)
        {
            var next = -1;
            var nearest = float.PositiveInfinity;
            for (var i = 0; i < _count; i++)
            {
                if (!_done[i] && _distance[i] < nearest)
                {
                    nearest = _distance[i];
                    next = i;
                }
            }

            if (next < 0)
            {
                break;
            }

            _done[next] = true;
            var row = next * _count;
            for (var j = 0; j < _count; j++)
            {
                var through = nearest + _weights[row + j];
                if (!_done[j] && through < _distance[j])
                {
                    _distance[j] = through;
                    _previous[j] = next;
                }
            }
        }

        var last = -1;
        var length = float.PositiveInfinity;
        for (var i = 0; i < _count; i++)
        {
            if (Touches(i, there))
            {
                var total = _distance[i] + Vector3.Distance(_centers[i], source);
                if (total < length)
                {
                    length = total;
                    last = i;
                }
            }
        }

        if (last < 0)
        {
            return new SoundPath(source, Vector3.Distance(listener, source), 0, false);
        }

        var steps = 0;
        for (var at = last; at >= 0 && steps < _count; at = _previous[at])
        {
            _route[steps++] = at;
        }

        var region = here;
        var openings = 0;
        for (var i = steps - 1; i >= 0; i--)
        {
            var doorway = _route[i];
            region = ReferenceEquals(_from[doorway], region) ? _to[doorway] : _from[doorway];
            openings += region.Kind == RegionKind.Room ? 1 : 0;
        }

        return new SoundPath(_centers[_route[steps - 1]], length, Math.Max(1, openings), true);
    }

    private bool Touches(int doorway, MapRegion region) =>
        ReferenceEquals(_from[doorway], region) || ReferenceEquals(_to[doorway], region);

    private bool SharesRegion(int a, int b) =>
        Touches(b, _from[a]) || Touches(b, _to[a]);
}
