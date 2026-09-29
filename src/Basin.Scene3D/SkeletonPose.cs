using System.Numerics;

namespace Basin.Scene3D;

public sealed class SkeletonPose
{
    private readonly Vector3[] _translations;
    private readonly Quaternion[] _rotations;
    private readonly Vector3[] _scales;

    public SkeletonPose(Skeleton skeleton)
    {
        Skeleton = skeleton;
        _translations = new Vector3[skeleton.NodeCount];
        _rotations = new Quaternion[skeleton.NodeCount];
        _scales = new Vector3[skeleton.NodeCount];
        World = new Matrix4x4[skeleton.NodeCount];
        Palette = new Matrix4x4[skeleton.Joints.Length];
        Reset();
        Resolve();
    }

    public Skeleton Skeleton { get; }

    public Matrix4x4[] World { get; }

    public Matrix4x4[] Palette { get; }

    public void Reset()
    {
        Skeleton.Translations.CopyTo(_translations, 0);
        Skeleton.Rotations.CopyTo(_rotations, 0);
        Skeleton.Scales.CopyTo(_scales, 0);
    }

    public void Apply(SkeletonClip clip, float time, float weight, bool loop)
    {
        if (weight <= 0 || clip.FrameCount == 0)
        {
            return;
        }

        weight = MathF.Min(weight, 1);
        var frames = clip.FrameCount;
        var position = time * clip.Rate;
        if (loop && frames > 1)
        {
            var span = frames - 1;
            position %= span;
            if (position < 0)
            {
                position += span;
            }
        }

        position = Math.Clamp(position, 0, frames - 1);
        var first = (int)MathF.Floor(position);
        var second = Math.Min(first + 1, frames - 1);
        var fraction = position - first;
        var nodes = clip.Nodes;
        for (var k = 0; k < nodes.Length; k++)
        {
            var a = (first * nodes.Length) + k;
            var b = (second * nodes.Length) + k;
            var node = nodes[k];
            var translation = Vector3.Lerp(clip.Translations[a], clip.Translations[b], fraction);
            var rotation = Quaternion.Slerp(clip.Rotations[a], clip.Rotations[b], fraction);
            var scale = Vector3.Lerp(clip.Scales[a], clip.Scales[b], fraction);
            if (weight >= 1)
            {
                _translations[node] = translation;
                _rotations[node] = rotation;
                _scales[node] = scale;
            }
            else
            {
                _translations[node] = Vector3.Lerp(_translations[node], translation, weight);
                _rotations[node] = Quaternion.Slerp(_rotations[node], rotation, weight);
                _scales[node] = Vector3.Lerp(_scales[node], scale, weight);
            }
        }
    }

    public void Resolve()
    {
        var parents = Skeleton.Parents;
        foreach (var node in Skeleton.Order)
        {
            var local = Matrix4x4.CreateScale(_scales[node]) *
                Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(_rotations[node])) *
                Matrix4x4.CreateTranslation(_translations[node]);
            World[node] = parents[node] >= 0 ? local * World[parents[node]] : local;
        }

        var joints = Skeleton.Joints;
        for (var j = 0; j < joints.Length; j++)
        {
            Palette[j] = Skeleton.InverseBinds[j] * World[joints[j]];
        }
    }
}
