namespace Basin.Physics;

public sealed class PhysicsWalls
{
    public const int Thickness = 1000;

    private readonly PhysicsWorld _world;
    private readonly List<Box> _fences = [];
    private readonly List<int> _xs = [];
    private readonly List<int> _ys = [];
    private PhysicsBody _body;

    public PhysicsWalls(PhysicsWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        _world = world;
    }

    public Box Bounds { get; private set; }

    public IReadOnlyList<Box> Fences => _fences;

    public PhysicsBody Body => _body;

    public void Rebuild(OutputLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        Span<Box> boxes = stackalloc Box[16];
        var count = 0;
        var list = boxes.Length >= layout.Outputs.Length ? boxes : new Box[layout.Outputs.Length];
        foreach (var (output, _) in layout.Outputs)
        {
            var box = layout.BoxOf(output);
            if (!box.IsEmpty)
            {
                list[count++] = box;
            }
        }

        Rebuild(list[..count]);
    }

    public void Rebuild(ReadOnlySpan<Box> areas)
    {
        Clear();
        if (areas.IsEmpty)
        {
            return;
        }

        var bounds = areas[0];
        for (var i = 1; i < areas.Length; i++)
        {
            bounds = Union(bounds, areas[i]);
        }

        Bounds = bounds;
        _body = _world.CreateWall();
        _world.AddWallBox(_body, new Box(bounds.X - Thickness, bounds.Y - Thickness, Thickness, bounds.Height + (2 * Thickness)), PhysicsCategory.Edges);
        _world.AddWallBox(_body, new Box(bounds.Right, bounds.Y - Thickness, Thickness, bounds.Height + (2 * Thickness)), PhysicsCategory.Edges);
        _world.AddWallBox(_body, new Box(bounds.X, bounds.Y - Thickness, bounds.Width, Thickness), PhysicsCategory.Edges);
        _world.AddWallBox(_body, new Box(bounds.X, bounds.Bottom, bounds.Width, Thickness), PhysicsCategory.Edges);

        DeadZones(areas, bounds);
        foreach (var fence in _fences)
        {
            _world.AddWallBox(_body, fence, PhysicsCategory.Fences);
        }
    }

    public void Clear()
    {
        _fences.Clear();
        Bounds = default;
        if (_body.IsValid)
        {
            _world.Destroy(_body);
        }

        _body = default;
    }

    private void DeadZones(ReadOnlySpan<Box> areas, in Box bounds)
    {
        _xs.Clear();
        _ys.Clear();
        _xs.Add(bounds.X);
        _xs.Add(bounds.Right);
        _ys.Add(bounds.Y);
        _ys.Add(bounds.Bottom);
        foreach (var area in areas)
        {
            _xs.Add(area.X);
            _xs.Add(area.Right);
            _ys.Add(area.Y);
            _ys.Add(area.Bottom);
        }

        Distinct(_xs);
        Distinct(_ys);
        for (var row = 0; row + 1 < _ys.Count; row++)
        {
            var top = _ys[row];
            var bottom = _ys[row + 1];
            var start = -1;
            for (var column = 0; column + 1 < _xs.Count; column++)
            {
                var cell = new Box(_xs[column], top, _xs[column + 1] - _xs[column], bottom - top);
                var dead = !Covered(areas, cell);
                if (dead && start < 0)
                {
                    start = column;
                }

                if (!dead && start >= 0)
                {
                    AddFence(_xs[start], top, _xs[column], bottom);
                    start = -1;
                }
            }

            if (start >= 0)
            {
                AddFence(_xs[start], top, _xs[^1], bottom);
            }
        }
    }

    private void AddFence(int left, int top, int right, int bottom)
    {
        var fence = new Box(left, top, right - left, bottom - top);
        for (var i = 0; i < _fences.Count; i++)
        {
            var above = _fences[i];
            if (above.X == fence.X && above.Width == fence.Width && above.Bottom == fence.Y)
            {
                _fences[i] = new Box(above.X, above.Y, above.Width, above.Height + fence.Height);
                return;
            }
        }

        _fences.Add(fence);
    }

    private static bool Covered(ReadOnlySpan<Box> areas, in Box cell)
    {
        var centerX = cell.X + (cell.Width / 2.0);
        var centerY = cell.Y + (cell.Height / 2.0);
        foreach (var area in areas)
        {
            if (centerX >= area.X && centerX < area.Right && centerY >= area.Y && centerY < area.Bottom)
            {
                return true;
            }
        }

        return false;
    }

    private static void Distinct(List<int> values)
    {
        values.Sort();
        var write = 0;
        for (var read = 0; read < values.Count; read++)
        {
            if (write == 0 || values[write - 1] != values[read])
            {
                values[write++] = values[read];
            }
        }

        values.RemoveRange(write, values.Count - write);
    }

    private static Box Union(in Box a, in Box b)
    {
        var left = Math.Min(a.X, b.X);
        var top = Math.Min(a.Y, b.Y);
        return new Box(left, top, Math.Max(a.Right, b.Right) - left, Math.Max(a.Bottom, b.Bottom) - top);
    }
}
