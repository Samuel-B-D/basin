using Basin.Kinematics;
using Basin.Scene;

namespace Basin.Effects;

public sealed class CanvasMotion
{
    private EffectTimeline _timeline;
    private double _from;
    private double _to;
    private double _current;
    private bool _running;
    private SpringHandoff _spring;
    private bool _springing;

    public CanvasMotion()
    {
        _timeline.Easing = EasingCurve.CubicBezier(0, 0, 0.2, 1);
    }

    public bool IsRunning => _running;

    public double Current => _current;

    public double Target => _to;

    public void Begin(double from, double to, long durationNanos)
    {
        _springing = false;
        _from = _running ? _current : from;
        _current = _from;
        _to = to;
        if (durationNanos <= 0 || _from == _to)
        {
            _current = _to;
            _running = false;
            return;
        }

        _running = true;
        _timeline.Start(durationNanos);
    }

    public void Settle(double from, double to, double velocity, double stiffness = 800, double epsilon = 0.001)
    {
        _from = _running ? _current : from;
        _current = _from;
        _to = to;
        _spring = new SpringHandoff(stiffness, 1.0) { Epsilon = epsilon };
        _spring.Start(_from, 0, velocity, 0, to, 0);
        _springing = _spring.IsMoving;
        _running = _springing;
        if (!_running)
        {
            _current = _to;
        }
    }

    public void Cancel() => _running = false;

    public bool Step(in FrameTick tick, out double value)
    {
        if (!_running)
        {
            value = _current;
            return false;
        }

        if (_springing)
        {
            var moving = _spring.Step(tick);
            var low = Math.Min(_from, _to);
            var high = Math.Max(_from, _to);
            _current = moving ? Math.Clamp(_spring.X, low, high) : _to;
            _running = moving;
            _springing = moving;
            value = _current;
            return moving;
        }

        var running = _timeline.Running(tick);
        var progress = _timeline.Progress(tick);
        _current = running ? _from + ((_to - _from) * progress) : _to;
        if (!running)
        {
            _running = false;
        }

        value = _current;
        return running;
    }
}
