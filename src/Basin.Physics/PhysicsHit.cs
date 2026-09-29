namespace Basin.Physics;

internal readonly record struct PhysicsHit(int SlotA, int SlotB, double X, double Y, double NormalX, double NormalY, double Speed);
