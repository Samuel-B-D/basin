namespace Basin.Kinematics;

public struct Fling
{
    public const double DefaultFriction = 4.0;

    public const double DefaultStopSpeed = 20.0;

    private TickDelta _clock;
    private double _x0;
    private double _y0;
    private double _vx0;
    private double _vy0;
    private long _elapsedNanos;
    private long _durationNanos;
    private bool _moving;

    public Fling(double friction, double stopSpeed)
    {
        Friction = friction;
        StopSpeed = stopSpeed;
    }

    public double Friction { get; set; }

    public double StopSpeed { get; set; }

    public readonly bool IsMoving => _moving;

    public double X { readonly get; private set; }

    public double Y { readonly get; private set; }

    public double VelocityX { readonly get; private set; }

    public double VelocityY { readonly get; private set; }

    public readonly (double X, double Y) ProjectedRest
    {
        get
        {
            if (!_moving)
            {
                return (X, Y);
            }

            var (x, y) = RestFrom(_x0, _y0, _vx0, _vy0, EffectiveFriction, EffectiveStopSpeed);
            return (x, y);
        }
    }

    private readonly double EffectiveFriction => Friction > 0 ? Friction : DefaultFriction;

    private readonly double EffectiveStopSpeed => StopSpeed > 0 ? StopSpeed : DefaultStopSpeed;

    public void Start(double x, double y, double velocityX, double velocityY)
    {
        _x0 = X = x;
        _y0 = Y = y;
        _vx0 = velocityX;
        _vy0 = velocityY;
        _elapsedNanos = 0;
        _clock.Reset();

        var speed = Math.Sqrt((velocityX * velocityX) + (velocityY * velocityY));
        var stop = EffectiveStopSpeed;
        _moving = speed > stop && double.IsFinite(speed);
        if (!_moving)
        {
            VelocityX = VelocityY = 0;
            _durationNanos = 0;
            return;
        }

        VelocityX = velocityX;
        VelocityY = velocityY;
        _durationNanos = (long)Math.Ceiling(Math.Log(speed / stop) / EffectiveFriction * 1e9);
    }

    public bool Step(in FrameTick tick)
    {
        if (!_moving)
        {
            return false;
        }

        Advance(_clock.Advance(tick));
        return _moving;
    }

    public void Advance(long nanos)
    {
        if (!_moving || nanos <= 0)
        {
            return;
        }

        _elapsedNanos += nanos;
        if (_elapsedNanos >= _durationNanos)
        {
            (X, Y) = RestFrom(_x0, _y0, _vx0, _vy0, EffectiveFriction, EffectiveStopSpeed);
            VelocityX = VelocityY = 0;
            _moving = false;
            return;
        }

        var k = EffectiveFriction;
        var decay = Math.Exp(-k * (_elapsedNanos / 1e9));
        var travel = (1 - decay) / k;
        X = _x0 + (_vx0 * travel);
        Y = _y0 + (_vy0 * travel);
        VelocityX = _vx0 * decay;
        VelocityY = _vy0 * decay;
    }

    public void Stop()
    {
        _moving = false;
        VelocityX = VelocityY = 0;
    }

    public static double RestDistance(double speed, double friction, double stopSpeed) =>
        speed <= stopSpeed || friction <= 0 ? 0 : (speed - stopSpeed) / friction;

    public static (double X, double Y) ThrowTo(
        double fromX, double fromY, double toX, double toY, double friction, double stopSpeed)
    {
        var dx = toX - fromX;
        var dy = toY - fromY;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        if (distance <= 0 || friction <= 0)
        {
            return (0, 0);
        }

        var speed = (distance * friction) + Math.Max(stopSpeed, 0);
        return (dx / distance * speed, dy / distance * speed);
    }

    private static (double X, double Y) RestFrom(
        double x, double y, double vx, double vy, double friction, double stopSpeed)
    {
        var speed = Math.Sqrt((vx * vx) + (vy * vy));
        if (speed <= stopSpeed)
        {
            return (x, y);
        }

        var scale = (speed - stopSpeed) / friction / speed;
        return (x + (vx * scale), y + (vy * scale));
    }
}
