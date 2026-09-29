namespace Basin.Physics;

public readonly record struct PhysicsMaterial(
    double Density = 1.0,
    double Friction = 0.3,
    double Restitution = 0.2,
    double GravityScale = 1.0,
    double LinearDamping = 1.0)
{
    public static PhysicsMaterial Default => new(1.0, 0.3, 0.2, 1.0, 1.0);
}
