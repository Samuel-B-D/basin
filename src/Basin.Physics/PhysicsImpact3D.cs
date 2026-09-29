using System.Numerics;

namespace Basin.Physics;

public readonly record struct PhysicsImpact3D(
    PhysicsBody3D A,
    PhysicsBody3D B,
    Vector3 Point,
    Vector3 Normal,
    double Speed);
