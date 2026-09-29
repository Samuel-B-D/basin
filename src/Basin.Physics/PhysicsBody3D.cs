using System.Numerics;

namespace Basin.Physics;

public readonly record struct PhysicsBody3D(PhysicsWorld3D? World, int Slot, int Generation)
{
    public bool IsValid => World is { } world && world.IsLive(this);

    public PhysicsPose3D Pose => Live.Pose(this);

    public PhysicsPose3D Current => Live.CurrentPose(this);

    public Vector3 Velocity => Live.VelocityOf(this).Linear;

    public Vector3 AngularVelocity => Live.VelocityOf(this).Angular;

    public bool IsAwake => Live.IsBodyAwake(this);

    public ulong Tag
    {
        get => Live.TagOf(this);
        set => Live.SetTag(this, value);
    }

    public double Mass => Live.MassOf(this);

    private PhysicsWorld3D Live => World ?? throw new InvalidOperationException("The physics body belongs to no world.");
}
