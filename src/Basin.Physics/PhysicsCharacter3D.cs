using System.Numerics;

namespace Basin.Physics;

public sealed class PhysicsCharacter3D
{
    internal PhysicsCharacter3D(PhysicsWorld3D world, PhysicsBody3D body, float radius, float height)
    {
        World = world;
        Body = body;
        Radius = radius;
        Height = height;
    }

    public PhysicsWorld3D World { get; }

    public PhysicsBody3D Body { get; }

    public float Radius { get; }

    public float Height { get; }

    public Vector3 Position => Body.Pose.Position;

    public Vector3 Velocity => Body.Velocity;

    public bool Supported => World.IsSupported(this);

    public void Move(Vector2 wish, Vector3 view) => World.SetCharacterGoal(this, wish, view, jump: false);

    public void Jump() => World.RequestJump(this);

    public void Teleport(Vector3 position) => World.Teleport(Body, PhysicsPose3D.At(position));
}
