using System.Numerics;

namespace Basin.Scene3D;

public sealed class SkeletonClip
{
    public SkeletonClip(
        string name, float duration, float rate, int[] nodes, Vector3[] translations, Quaternion[] rotations, Vector3[] scales)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(rate, 0);
        var frames = nodes.Length == 0 ? 0 : translations.Length / nodes.Length;
        if (translations.Length != frames * nodes.Length || rotations.Length != translations.Length || scales.Length != translations.Length)
        {
            throw new ArgumentException("Every frame must hold one pose per animated node.");
        }

        Name = name;
        Duration = duration;
        Rate = rate;
        Nodes = nodes;
        Translations = translations;
        Rotations = rotations;
        Scales = scales;
        FrameCount = frames;
    }

    public string Name { get; }

    public float Duration { get; }

    public float Rate { get; }

    public int[] Nodes { get; }

    public int FrameCount { get; }

    public Vector3[] Translations { get; }

    public Quaternion[] Rotations { get; }

    public Vector3[] Scales { get; }
}
