using System.Numerics;

namespace Basin.Physics;

public abstract class PhysicsEngine3D
{
    private protected PhysicsEngine3D()
    {
    }

    public abstract string Name { get; }

    internal abstract IPhysicsBackend3D CreateBackend(Vector3 gravity, float impactThreshold);
}
