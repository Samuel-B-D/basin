using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;

namespace Basin.Physics;

internal struct BepuNarrowPhase : INarrowPhaseCallbacks
{
    public const float ContactFrequency = 30f;

    public const float BounceFrequency = 10f;

    public BepuState State;

    public readonly void Initialize(Simulation simulation) => State.Simulation = simulation;

    public readonly bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b, ref float speculativeMargin)
    {
        if ((a.Mobility != CollidableMobility.Dynamic && b.Mobility != CollidableMobility.Dynamic) || !State.Collides(a, b))
        {
            return false;
        }

        speculativeMargin = MathF.Min(speculativeMargin, State.Margin(a, b));
        return true;
    }

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
        pairMaterial.MaximumRecoveryVelocity = restitution > 0 ? MathF.Max(BepuState.DefaultRecovery, begin) : BepuState.DefaultRecovery;
        pairMaterial.SpringSettings = new SpringSettings(Frequency(restitution), DampingRatio(restitution));
        return true;
    }

    public readonly bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB, ref ConvexContactManifold manifold) => true;

    public readonly void Dispose()
    {
    }

    public static float Frequency(float restitution) =>
        ContactFrequency + ((BounceFrequency - ContactFrequency) * Math.Clamp(restitution, 0, 1));

    public static float DampingRatio(float restitution)
    {
        if (restitution <= 0)
        {
            return 1;
        }

        if (restitution >= 1)
        {
            return 0;
        }

        var log = MathF.Log(restitution);
        return -log / MathF.Sqrt((MathF.PI * MathF.PI) + (log * log));
    }
}
