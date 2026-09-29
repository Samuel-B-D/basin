using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;

namespace Basin.Physics;

internal struct Bepu3DNarrowPhase : INarrowPhaseCallbacks
{
    public Bepu3DState State;

    public CharacterControllers Characters;

    public readonly void Initialize(Simulation simulation)
    {
        State.Simulation = simulation;
        Characters.Initialize(simulation);
    }

    public readonly bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b, ref float speculativeMargin) =>
        a.Mobility == CollidableMobility.Dynamic || b.Mobility == CollidableMobility.Dynamic;

    public readonly bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB) => true;

    public readonly bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties pairMaterial)
        where TManifold : unmanaged, IContactManifold<TManifold>
    {
        State.Material(pair.A, out var frictionA, out var restitutionA);
        State.Material(pair.B, out var frictionB, out var restitutionB);
        var restitution = MathF.Max(restitutionA, restitutionB);
        var count = manifold.Count;
        var deepest = 0;
        var depth = float.MinValue;
        for (var i = 0; i < count; i++)
        {
            var candidate = manifold.GetDepth(i);
            if (candidate > depth)
            {
                depth = candidate;
                deepest = i;
            }
        }

        var offset = count > 0 ? manifold.GetOffset(deepest) : Vector3.Zero;
        var normal = count > 0 ? manifold.GetNormal(deepest) : Vector3.Zero;
        var begin = State.Track(pair, count, offset, normal, depth);
        pairMaterial.FrictionCoefficient = MathF.Sqrt(frictionA * frictionB);
        pairMaterial.MaximumRecoveryVelocity = restitution > 0 ? MathF.Max(Bepu3DState.DefaultRecovery, begin) : Bepu3DState.DefaultRecovery;
        pairMaterial.SpringSettings = new SpringSettings(BepuNarrowPhase.Frequency(restitution), BepuNarrowPhase.DampingRatio(restitution));
        Characters.TryReportContacts(pair, ref manifold, workerIndex, ref pairMaterial);
        return true;
    }

    public readonly bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB, ref ConvexContactManifold manifold) => true;

    public readonly void Dispose() => Characters.Dispose();
}
