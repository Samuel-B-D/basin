using System.Runtime.CompilerServices;

namespace Basin.Seat;

public struct VelocityTracker
{
    public const int Capacity = 8;

    public const double Smoothing = 0.4;

    public const long StaleNanos = 60_000_000;

    private Ring _ring;
    private int _head;
    private int _count;
    private double _vx;
    private double _vy;
    private bool _hasVelocity;

    public long MinSpanNanos { get; set; }

    public readonly int Count => _count;

    public readonly bool HasVelocity => _hasVelocity;

    public readonly (double X, double Y) Smoothed => _hasVelocity ? (_vx, _vy) : (0, 0);

    public readonly long NewestNanos => _count == 0 ? 0 : _ring[Newest].Nanos;

    private readonly int Newest => (_head + Capacity - 1) % Capacity;

    public void Add(double x, double y, long nanos)
    {
        if (_count > 0)
        {
            ref var newest = ref _ring[Newest];
            var elapsed = nanos - newest.Nanos;
            if (elapsed <= 0)
            {
                newest.X = x;
                newest.Y = y;
                return;
            }

            if (elapsed >= StaleNanos)
            {
                _count = 0;
                _hasVelocity = false;
            }
            else
            {
                var partner = _ring[Partner(nanos)];
                var span = (nanos - partner.Nanos) / 1e9;
                var ix = (x - partner.X) / span;
                var iy = (y - partner.Y) / span;
                if (_hasVelocity)
                {
                    _vx += Smoothing * (ix - _vx);
                    _vy += Smoothing * (iy - _vy);
                }
                else
                {
                    _vx = ix;
                    _vy = iy;
                    _hasVelocity = true;
                }
            }
        }

        _ring[_head] = new Sample { X = x, Y = y, Nanos = nanos };
        _head = (_head + 1) % Capacity;
        if (_count < Capacity)
        {
            _count++;
        }
    }

    public readonly (double X, double Y) Velocity(long nowNanos) =>
        _hasVelocity && _count > 0 && nowNanos - _ring[Newest].Nanos < StaleNanos ? (_vx, _vy) : (0, 0);

    public void Reset()
    {
        _head = 0;
        _count = 0;
        _vx = 0;
        _vy = 0;
        _hasVelocity = false;
    }

    private readonly int Partner(long nanos)
    {
        var index = Newest;
        for (var i = 1; i < _count; i++)
        {
            if (nanos - _ring[index].Nanos >= MinSpanNanos)
            {
                break;
            }

            var older = (index + Capacity - 1) % Capacity;
            if (nanos - _ring[older].Nanos >= StaleNanos)
            {
                break;
            }

            index = older;
        }

        return index;
    }

    private struct Sample
    {
        public double X;
        public double Y;
        public long Nanos;
    }

    [InlineArray(Capacity)]
    private struct Ring
    {
        private Sample _element;
    }
}
