using Basin;
using Basin.Kinematics;

namespace TinyComp;

internal sealed class GridPinch
{
    public const int Zones = 4;

    private const double CenterRest = 0.5;

    private const double DepthRest = 0.002;

    private readonly SpringMotion[] _centers = new SpringMotion[Zones];
    private readonly SpringMotion[] _depths = new SpringMotion[Zones];
    private readonly double[] _rest = new double[Zones];
    private readonly double[] _targetCenter = new double[Zones];
    private readonly double[] _targetDepth = new double[Zones];
    private readonly object?[] _sources = new object?[Zones];
    private readonly bool[] _engaged = new bool[Zones];
    private TickDelta _delta;

    public bool Moving { get; private set; }

    public static double Score(double speed, double influence) =>
        influence <= 0 ? 0.0 : double.IsPositiveInfinity(speed) ? double.PositiveInfinity : speed * influence;

    public void Rest(int zone, double center) => _rest[zone] = center;

    public void Aim(int zone, double center, double depth, object source)
    {
        _targetCenter[zone] = center;
        _targetDepth[zone] = depth;
        _sources[zone] = source;
    }

    public void Release(int zone)
    {
        _targetCenter[zone] = _rest[zone];
        _targetDepth[zone] = 0.0;
        _sources[zone] = null;
    }

    public bool Engaged(int zone) => _engaged[zone];

    public object? SourceOf(int zone) => _sources[zone];

    public double CenterOf(int zone) => _engaged[zone] ? _centers[zone].Position : _rest[zone];

    public double DepthOf(int zone) => _engaged[zone] ? _depths[zone].Position : 0.0;

    public bool Step(in FrameTick tick, double spring, double damping)
    {
        var millis = _delta.Advance(tick) / 1_000_000.0;
        var changed = false;
        var moving = false;
        for (var zone = 0; zone < Zones; zone++)
        {
            if (!_engaged[zone])
            {
                if (_targetDepth[zone] == 0.0)
                {
                    continue;
                }

                _engaged[zone] = true;
                _centers[zone] = default;
                _centers[zone].SetPosition(_rest[zone]);
                _depths[zone] = default;
                changed = true;
            }

            ref var center = ref _centers[zone];
            ref var depth = ref _depths[zone];
            center.SpringConstant = spring;
            center.DampingRatio = damping;
            center.Epsilon = CenterRest;
            center.Anchor = _targetCenter[zone];
            depth.SpringConstant = spring;
            depth.DampingRatio = damping;
            depth.Epsilon = DepthRest;
            depth.Anchor = _targetDepth[zone];
            if (center.IsMoving)
            {
                center.Advance(millis);
                changed = true;
            }

            if (depth.IsMoving)
            {
                depth.Advance(millis);
                changed = true;
            }

            if (!center.IsMoving && center.Position != center.Anchor)
            {
                center.SetPosition(center.Anchor);
                changed = true;
            }

            if (!depth.IsMoving && depth.Position != depth.Anchor)
            {
                depth.SetPosition(depth.Anchor);
                changed = true;
            }

            if (_targetDepth[zone] == 0.0 && !depth.IsMoving && !center.IsMoving)
            {
                _engaged[zone] = false;
                changed = true;
                continue;
            }

            moving |= center.IsMoving || depth.IsMoving;
        }

        Moving = moving;
        return changed;
    }

    public bool Reset()
    {
        _delta.Reset();
        Moving = false;
        var any = false;
        for (var zone = 0; zone < Zones; zone++)
        {
            any |= _engaged[zone];
            _engaged[zone] = false;
            _sources[zone] = null;
            _targetDepth[zone] = 0.0;
        }

        return any;
    }
}
