using System.Numerics;

namespace DungeonWm;

internal readonly record struct SoundPath(Vector3 Apparent, float Length, int Doorways, bool Audible)
{
    public static SoundPath Direct(Vector3 listener, Vector3 source) => new(source, Vector3.Distance(listener, source), 0, true);
}
