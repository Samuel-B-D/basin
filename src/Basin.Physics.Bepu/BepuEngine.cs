namespace Basin.Physics;

public sealed class BepuEngine : PhysicsEngine
{
    private BepuEngine()
    {
    }

    public static BepuEngine Instance { get; } = new();

    public override string Name => "bepu";

    public override double DecayRate(double linearDamping) => linearDamping;

    internal override IPhysicsBackend CreateBackend(double gravityX, double gravityY, double impactThreshold) =>
        new BepuBackend(gravityX, gravityY, impactThreshold);
}
