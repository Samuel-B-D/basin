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

internal struct CharacterMotionAccumulatedImpulse
{
    public Vector2Wide Horizontal;
    public Vector<float> Vertical;
}
