#nullable disable
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuUtilities;
using BepuUtilities.Collections;
using BepuUtilities.Memory;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Basin.Physics;

internal struct CharacterController
{
    public Vector3 ViewDirection;
    public Vector2 TargetVelocity;
    public bool TryJump;

    public BodyHandle BodyHandle;
    public Vector3 LocalUp;
    public float JumpVelocity;
    public float MaximumHorizontalForce;
    public float MaximumVerticalForce;
    public float CosMaximumSlope;
    public float MinimumSupportDepth;
    public float MinimumSupportContinuationDepth;

    public bool Supported;
    public CollidableReference Support;
    public ConstraintHandle MotionConstraintHandle;
}
