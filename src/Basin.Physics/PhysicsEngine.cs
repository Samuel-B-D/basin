namespace Basin.Physics;

public abstract class PhysicsEngine
{
    private protected PhysicsEngine()
    {
    }

    public abstract string Name { get; }

    public abstract double DecayRate(double linearDamping);

    internal abstract IPhysicsBackend CreateBackend(double gravityX, double gravityY, double impactThreshold);
}
