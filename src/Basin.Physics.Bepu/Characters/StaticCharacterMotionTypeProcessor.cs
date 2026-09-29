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

internal class StaticCharacterMotionTypeProcessor : OneBodyTypeProcessor<StaticCharacterMotionPrestep, CharacterMotionAccumulatedImpulse, StaticCharacterMotionFunctions, AccessAll, AccessAll>
{
    public const int BatchTypeId = 50;
}
