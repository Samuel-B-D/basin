namespace Basin.Physics;

[Flags]
public enum PhysicsCategory : ulong
{
    None = 0,
    Windows = 1,
    Edges = 2,
    Fences = 4,
    All = Windows | Edges | Fences,
}
