using System.Numerics;

namespace Basin.Scene3D;

public sealed class Skeleton
{
    public const int MaxJoints = 48;

    public Skeleton(
        string[] names,
        int[] parents,
        Vector3[] translations,
        Quaternion[] rotations,
        Vector3[] scales,
        int[] joints,
        Matrix4x4[] inverseBinds)
    {
        var count = names.Length;
        if (parents.Length != count || translations.Length != count || rotations.Length != count || scales.Length != count)
        {
            throw new ArgumentException("Every node array must have one entry per node.");
        }

        if (joints.Length != inverseBinds.Length)
        {
            throw new ArgumentException("Every joint needs an inverse bind matrix.");
        }

        Names = names;
        Parents = parents;
        Translations = translations;
        Rotations = rotations;
        Scales = scales;
        Joints = joints;
        InverseBinds = inverseBinds;
        Order = TopologicalOrder(parents);
    }

    public string[] Names { get; }

    public int[] Parents { get; }

    public Vector3[] Translations { get; }

    public Quaternion[] Rotations { get; }

    public Vector3[] Scales { get; }

    public int[] Joints { get; }

    public Matrix4x4[] InverseBinds { get; }

    public int[] Order { get; }

    public int NodeCount => Names.Length;

    public int IndexOf(string name) => Array.IndexOf(Names, name);

    private static int[] TopologicalOrder(int[] parents)
    {
        var order = new List<int>(parents.Length);
        var placed = new bool[parents.Length];
        while (order.Count < parents.Length)
        {
            var progressed = false;
            for (var i = 0; i < parents.Length; i++)
            {
                if (!placed[i] && (parents[i] < 0 || placed[parents[i]]))
                {
                    placed[i] = true;
                    order.Add(i);
                    progressed = true;
                }
            }

            if (!progressed)
            {
                throw new ArgumentException("The node hierarchy has a cycle.");
            }
        }

        return [.. order];
    }
}
