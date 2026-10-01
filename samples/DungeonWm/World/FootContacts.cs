using Basin.Scene3D;

namespace DungeonWm;

internal sealed class FootContacts
{
    private const int Samples = 120;

    private FootContacts(float duration, float[] times, int[] feet)
    {
        Duration = duration;
        Times = times;
        Feet = feet;
    }

    public float Duration { get; }

    public float[] Times { get; }

    public int[] Feet { get; }

    public static FootContacts? Find(Skeleton skeleton, SkeletonClip? clip)
    {
        if (clip is null || clip.Duration <= 0)
        {
            return null;
        }

        int[] joints = [skeleton.IndexOf("foot.l"), skeleton.IndexOf("foot.r")];
        if (joints[0] < 0 || joints[1] < 0)
        {
            return null;
        }

        var heights = new float[2, Samples];
        var pose = new SkeletonPose(skeleton);
        for (var i = 0; i < Samples; i++)
        {
            pose.Reset();
            pose.Apply(clip, clip.Duration * i / Samples, 1, loop: true);
            pose.Resolve();
            heights[0, i] = pose.World[joints[0]].M42;
            heights[1, i] = pose.World[joints[1]].M42;
        }

        var times = new List<(float Time, int Foot)>();
        for (var foot = 0; foot < 2; foot++)
        {
            float low = float.MaxValue, high = float.MinValue;
            for (var i = 0; i < Samples; i++)
            {
                low = MathF.Min(low, heights[foot, i]);
                high = MathF.Max(high, heights[foot, i]);
            }

            var down = low + ((high - low) * 0.15f);
            for (var i = 0; i < Samples; i++)
            {
                var before = heights[foot, (i + Samples - 1) % Samples];
                if (before > down && heights[foot, i] <= down)
                {
                    times.Add((clip.Duration * i / Samples, foot));
                }
            }
        }

        times.Sort((a, b) => a.Time.CompareTo(b.Time));
        return times.Count == 0 ? null : new FootContacts(clip.Duration, [.. times.Select(t => t.Time)], [.. times.Select(t => t.Foot)]);
    }

    public int Crossed(float from, float to)
    {
        if (to <= from)
        {
            return -1;
        }

        var start = from % Duration;
        var span = to - from;
        var found = -1;
        for (var i = 0; i < Times.Length; i++)
        {
            var ahead = Times[i] - start;
            if (ahead <= 0)
            {
                ahead += Duration;
            }

            if (ahead <= span)
            {
                found = Feet[i];
            }
        }

        return found;
    }
}
