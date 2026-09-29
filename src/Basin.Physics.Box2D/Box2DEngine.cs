namespace Basin.Physics;

public sealed class Box2DEngine : PhysicsEngine
{
    private Box2DEngine()
    {
    }

    public static Box2DEngine Instance { get; } = new();

    public override string Name => "box2d";

    public override double DecayRate(double linearDamping)
    {
        var sub = PhysicsWorld.StepNanos / 1e9 / PhysicsWorld.SubSteps;
        return Math.Log(1 + (sub * linearDamping)) / sub;
    }

    internal override IPhysicsBackend CreateBackend(double gravityX, double gravityY, double impactThreshold) =>
        new Box2DBackend(gravityX, gravityY, impactThreshold);
}
