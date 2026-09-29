namespace Basin.Physics;

public readonly record struct PhysicsImpact(
    PhysicsBody A,
    PhysicsBody B,
    double X,
    double Y,
    double NormalX,
    double NormalY,
    double Speed);
