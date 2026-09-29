#nullable disable
using BepuUtilities;
using BepuPhysics;
using System.Numerics;
using System;
using System.Runtime.CompilerServices;
using BepuPhysics.Constraints;
using BepuUtilities.Memory;
using static BepuUtilities.GatherScatter;

namespace Basin.Physics;

internal struct StaticCharacterMotionPrestep
{
    public QuaternionWide SurfaceBasis;
    public Vector<float> MaximumHorizontalForce;
    public Vector<float> MaximumVerticalForce;
	public Vector<float> Depth;
    public Vector2Wide TargetVelocity;
    public Vector3Wide OffsetFromCharacter;

}
