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

internal class DynamicCharacterMotionTypeProcessor : TwoBodyTypeProcessor<DynamicCharacterMotionPrestep, CharacterMotionAccumulatedImpulse, DynamicCharacterMotionFunctions, AccessAll, AccessAll, AccessAll, AccessAll>
{
    public const int BatchTypeId = 51;
}
