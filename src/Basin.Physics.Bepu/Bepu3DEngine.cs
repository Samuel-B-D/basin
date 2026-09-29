using System.Numerics;

namespace Basin.Physics;

public sealed class Bepu3DEngine : PhysicsEngine3D
{
    private Bepu3DEngine()
    {
    }

    public static Bepu3DEngine Instance { get; } = new();

    public override string Name => "bepu";

    internal override IPhysicsBackend3D CreateBackend(Vector3 gravity, float impactThreshold) =>
        new Bepu3DBackend(gravity, impactThreshold);
}
