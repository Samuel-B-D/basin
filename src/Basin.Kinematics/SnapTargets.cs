using System.Runtime.CompilerServices;

namespace Basin.Kinematics;

public struct SnapTargets
{
    public const int Capacity = 16;

    private Slots _slots;
    private int _count;

    public readonly int Count => _count;

    public bool Add(in SnapTarget target)
    {
        if (_count >= Capacity)
        {
            return false;
        }

        _slots[_count++] = target;
        return true;
    }

    public void Clear() => _count = 0;

    public readonly bool Nearest(double x, double y, out double snappedX, out double snappedY)
    {
        snappedX = x;
        snappedY = y;
        var best = double.MaxValue;
        for (var i = 0; i < _count; i++)
        {
            var (sx, sy) = _slots[i].Nearest(x, y);
            var dx = sx - x;
            var dy = sy - y;
            var distance = (dx * dx) + (dy * dy);
            if (distance < best)
            {
                best = distance;
                snappedX = sx;
                snappedY = sy;
            }
        }

        return _count > 0;
    }

    public readonly bool Handoff(in Fling fling, ref SpringHandoff spring)
    {
        var (restX, restY) = fling.ProjectedRest;
        if (!Nearest(restX, restY, out var targetX, out var targetY))
        {
            return false;
        }

        spring.Start(fling.X, fling.Y, fling.VelocityX, fling.VelocityY, targetX, targetY);
        return true;
    }

    [InlineArray(Capacity)]
    private struct Slots
    {
        private SnapTarget _element;
    }
}
