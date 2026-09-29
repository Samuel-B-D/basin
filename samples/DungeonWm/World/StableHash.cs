namespace DungeonWm;

internal static class StableHash
{
    public static int Of(int a, int b = 0, int c = 0, int d = 0)
    {
        var hash = 2166136261u;
        hash = Step(hash, a);
        hash = Step(hash, b);
        hash = Step(hash, c);
        hash = Step(hash, d);
        hash ^= hash >> 16;
        hash *= 0x7feb352d;
        hash ^= hash >> 15;
        hash *= 0x846ca68b;
        hash ^= hash >> 16;
        return (int)(hash & 0x7FFFFFFF);
    }

    private static uint Step(uint hash, int value)
    {
        unchecked
        {
            hash ^= (uint)value;
            hash *= 16777619u;
            hash ^= (uint)value >> 13;
            return hash * 16777619u;
        }
    }
}
