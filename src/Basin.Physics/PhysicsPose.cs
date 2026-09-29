namespace Basin.Physics;

public readonly record struct PhysicsPose(double X, double Y, double Angle, double PivotX, double PivotY)
{
    public bool IsUpright => Angle == 0;

    public RenderTransform Rotation => Angle == 0
        ? RenderTransform.Identity
        : RenderTransform.RotationAbout(Angle, PivotX, PivotY);

    public (double X, double Y) ToLocal(double x, double y)
    {
        var dx = x - X - PivotX;
        var dy = y - Y - PivotY;
        var cos = Math.Cos(-Angle);
        var sin = Math.Sin(-Angle);
        return (PivotX + (cos * dx) - (sin * dy), PivotY + (sin * dx) + (cos * dy));
    }
}
