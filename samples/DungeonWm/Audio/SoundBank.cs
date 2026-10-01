using Basin.Audio.PipeWire;

namespace DungeonWm;

internal sealed class SoundBank
{
    private readonly Random _random = new(0x50d);

    private SoundBank(Dictionary<string, SoundClip[]> groups)
    {
        Steps = groups["footstep"];
        Jumps = groups["cloth"];
        Landings = groups["impactSoft_heavy"];
        LightImpacts = groups["impactWood_medium"];
        HeavyImpacts = groups["impactWood_heavy"];
        Latch = groups["metalLatch"][0];
        Click = groups["metalClick"][0];
    }

    public SoundClip[] Steps { get; }

    public SoundClip[] Jumps { get; }

    public SoundClip[] Landings { get; }

    public SoundClip[] LightImpacts { get; }

    public SoundClip[] HeavyImpacts { get; }

    public SoundClip Latch { get; }

    public SoundClip Click { get; }

    public static SoundBank Load()
    {
        var assembly = typeof(SoundBank).Assembly;
        var groups = new Dictionary<string, List<SoundClip>>(StringComparer.Ordinal);
        foreach (var resource in assembly.GetManifestResourceNames().Order(StringComparer.Ordinal))
        {
            if (!resource.StartsWith("Kenney/", StringComparison.Ordinal) || !resource.EndsWith(".wav", StringComparison.Ordinal))
            {
                continue;
            }

            var name = Path.GetFileNameWithoutExtension(resource);
            var group = name.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9').TrimEnd('_');
            using var stream = assembly.GetManifestResourceStream(resource)!;
            if (!groups.TryGetValue(group, out var clips))
            {
                clips = [];
                groups[group] = clips;
            }

            clips.Add(SoundClip.FromWav(name, stream));
        }

        return new SoundBank(groups.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal));
    }

    public SoundClip Pick(SoundClip[] clips) => clips[_random.Next(clips.Length)];

    public SoundClip PickOther(SoundClip[] clips, SoundClip? last)
    {
        var clip = clips[_random.Next(clips.Length)];
        return clips.Length > 1 && ReferenceEquals(clip, last) ? clips[(Array.IndexOf(clips, clip) + 1 + _random.Next(clips.Length - 1)) % clips.Length] : clip;
    }

    public float Between(float low, float high) => low + ((float)_random.NextDouble() * (high - low));

    public float Jitter(float decibels) => ((float)_random.NextDouble() * 2 - 1) * decibels;
}
