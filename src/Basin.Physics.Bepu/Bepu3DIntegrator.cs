using System.Numerics;
using BepuPhysics;
using BepuUtilities;

namespace Basin.Physics;

internal struct Bepu3DIntegrator : IPoseIntegratorCallbacks
{
    public Bepu3DState State;

    public readonly AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;

    public readonly bool AllowSubstepsForUnconstrainedBodies => false;

    public readonly bool IntegrateVelocityForKinematics => false;

    public readonly void Initialize(Simulation simulation)
    {
    }

    public readonly void PrepareForIntegration(float dt)
    {
    }

    public readonly void IntegrateVelocity(
        Vector<int> bodyIndices,
        Vector3Wide position,
        QuaternionWide orientation,
        BodyInertiaWide localInertia,
        Vector<int> integrationMask,
        int workerIndex,
        Vector<float> dt,
        ref BodyVelocityWide velocity)
    {
        var state = State;
        var handles = state.Simulation!.Bodies.ActiveSet.IndexToHandle;
        var lanes = Vector<float>.Count;
        Span<float> pushX = stackalloc float[lanes];
        Span<float> pushY = stackalloc float[lanes];
        Span<float> pushZ = stackalloc float[lanes];
        Span<float> keep = stackalloc float[lanes];
        Span<float> keepSpin = stackalloc float[lanes];
        for (var lane = 0; lane < lanes; lane++)
        {
            if (integrationMask[lane] == 0)
            {
                pushX[lane] = pushY[lane] = pushZ[lane] = 0;
                keep[lane] = keepSpin[lane] = 1;
                continue;
            }

            var handle = handles[bodyIndices[lane]].Value;
            var seconds = dt[lane];
            var scale = state.GravityScale[handle] * seconds;
            var damping = state.Damping[handle];
            pushX[lane] = state.Gravity.X * scale;
            pushY[lane] = state.Gravity.Y * scale;
            pushZ[lane] = state.Gravity.Z * scale;
            keep[lane] = MathF.Exp(-damping * seconds);
            keepSpin[lane] = MathF.Exp(-(damping + 0.5f) * seconds);
        }

        var linearKeep = new Vector<float>(keep);
        var spinKeep = new Vector<float>(keepSpin);
        velocity.Linear.X = (velocity.Linear.X + new Vector<float>(pushX)) * linearKeep;
        velocity.Linear.Y = (velocity.Linear.Y + new Vector<float>(pushY)) * linearKeep;
        velocity.Linear.Z = (velocity.Linear.Z + new Vector<float>(pushZ)) * linearKeep;
        velocity.Angular.X *= spinKeep;
        velocity.Angular.Y *= spinKeep;
        velocity.Angular.Z *= spinKeep;
    }
}
