namespace Basin.Kinematics;

public struct SpringHandoff
{
    public const double DefaultStiffness = 200.0;

    public const double DefaultDampingRatio = 1.0;

    public const double DefaultEpsilon = 0.5;

    private SpringMotion _x;
    private SpringMotion _y;
    private TickDelta _clock;
    private bool _moving;

    public SpringHandoff(double stiffness, double dampingRatio)
    {
        Stiffness = stiffness;
        DampingRatio = dampingRatio;
    }

    public double Stiffness { get; set; }

    public double DampingRatio { get; set; }

    public double Epsilon { get; set; }

    public readonly bool IsMoving => _moving;

    public double TargetX { readonly get; private set; }

    public double TargetY { readonly get; private set; }

    public readonly double X => _moving ? _x.Position : TargetX;

    public readonly double Y => _moving ? _y.Position : TargetY;

    public readonly double VelocityX => _moving ? _x.Velocity : 0;

    public readonly double VelocityY => _moving ? _y.Velocity : 0;

    public void Start(double x, double y, double velocityX, double velocityY, double targetX, double targetY)
    {
        var stiffness = Stiffness > 0 ? Stiffness : DefaultStiffness;
        var damping = DampingRatio > 0 ? DampingRatio : DefaultDampingRatio;
        var epsilon = Epsilon > 0 ? Epsilon : DefaultEpsilon;
        _x = new SpringMotion(stiffness, damping) { Epsilon = epsilon, Anchor = targetX };
        _y = new SpringMotion(stiffness, damping) { Epsilon = epsilon, Anchor = targetY };
        _x.SetPosition(x);
        _x.SetVelocity(velocityX);
        _y.SetPosition(y);
        _y.SetVelocity(velocityY);
        TargetX = targetX;
        TargetY = targetY;
        _clock.Reset();
        _moving = _x.IsMoving || _y.IsMoving;
    }

    public void Retarget(double targetX, double targetY)
    {
        TargetX = targetX;
        TargetY = targetY;
        if (!_moving)
        {
            return;
        }

        _x.Anchor = targetX;
        _y.Anchor = targetY;
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

        var millis = nanos / 1e6;
        _x.Advance(millis);
        _y.Advance(millis);
        _moving = _x.IsMoving || _y.IsMoving;
    }

    public void Stop() => _moving = false;
}
