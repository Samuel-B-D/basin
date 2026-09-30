using System.Numerics;
using System.Runtime.CompilerServices;

namespace Basin.Kinematics;

public struct VelocityTracker3
{
    public const int Capacity = 8;

    public const float Smoothing = 0.4f;

    public const long StaleNanos = 60_000_000;

    private Ring _ring;
    private int _head;
    private int _count;
    private Vector3 _velocity;
    private bool _hasVelocity;

    public long MinSpanNanos { get; set; }

    public readonly int Count => _count;

    public readonly bool HasVelocity => _hasVelocity;

    public readonly Vector3 Smoothed => _hasVelocity ? _velocity : Vector3.Zero;

    public readonly long NewestNanos => _count == 0 ? 0 : _ring[Newest].Nanos;

    private readonly int Newest => (_head + Capacity - 1) % Capacity;

    public void Add(Vector3 point, long nanos)
    {
        if (_count > 0)
        {
            ref var newest = ref _ring[Newest];
            var elapsed = nanos - newest.Nanos;
            if (elapsed <= 0)
            {
                newest.Point = point;
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
                var instant = (point - partner.Point) / (float)((nanos - partner.Nanos) / 1e9);
                if (_hasVelocity)
                {
                    _velocity += Smoothing * (instant - _velocity);
                }
                else
                {
                    _velocity = instant;
                    _hasVelocity = true;
                }
            }
        }

        _ring[_head] = new Sample { Point = point, Nanos = nanos };
        _head = (_head + 1) % Capacity;
        if (_count < Capacity)
        {
            _count++;
        }
    }

    public readonly Vector3 Velocity(long nowNanos) =>
        _hasVelocity && _count > 0 && nowNanos - _ring[Newest].Nanos < StaleNanos ? _velocity : Vector3.Zero;

    public void Reset()
    {
        _head = 0;
        _count = 0;
        _velocity = Vector3.Zero;
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
        public Vector3 Point;
        public long Nanos;
    }

    [InlineArray(Capacity)]
    private struct Ring
    {
        private Sample _element;
    }
}
