using System.Numerics;
using BepuPhysics;
using BepuUtilities;

namespace Basin.Physics;

internal struct BepuIntegrator : IPoseIntegratorCallbacks
{
    public BepuState State;

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
        Span<float> keepX = stackalloc float[lanes];
        Span<float> keepY = stackalloc float[lanes];
        Span<float> keepSpin = stackalloc float[lanes];
        for (var lane = 0; lane < lanes; lane++)
        {
            if (integrationMask[lane] == 0)
            {
                pushX[lane] = pushY[lane] = 0;
                keepX[lane] = keepY[lane] = keepSpin[lane] = 1;
                continue;
            }

            var handle = handles[bodyIndices[lane]].Value;
            var seconds = dt[lane];
            var scale = state.GravityScale[handle] * seconds;
            var damping = state.Damping[handle];
            var locks = state.Locks[handle];
            var keep = MathF.Exp(-damping * seconds);
            pushX[lane] = state.GravityX * scale;
            pushY[lane] = state.GravityY * scale;
            keepX[lane] = (locks & PhysicsLocks.X) != 0 ? 0 : keep;
            keepY[lane] = (locks & PhysicsLocks.Y) != 0 ? 0 : keep;
            keepSpin[lane] = (locks & PhysicsLocks.Rotation) != 0 ? 0 : MathF.Exp(-(damping + 1) * seconds);
        }

        velocity.Linear.X = (velocity.Linear.X + new Vector<float>(pushX)) * new Vector<float>(keepX);
        velocity.Linear.Y = (velocity.Linear.Y + new Vector<float>(pushY)) * new Vector<float>(keepY);
        velocity.Linear.Z = Vector<float>.Zero;
        velocity.Angular.X = Vector<float>.Zero;
        velocity.Angular.Y = Vector<float>.Zero;
        velocity.Angular.Z *= new Vector<float>(keepSpin);
    }
}
