using System.Numerics;

namespace Basin.Physics;

internal readonly record struct PhysicsHit3D(int SlotA, int SlotB, Vector3 Point, Vector3 Normal, float Speed);
