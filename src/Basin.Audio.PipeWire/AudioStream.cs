namespace Basin.Audio.PipeWire;

public sealed class AudioStream
{
    public AudioStream(uint nodeId)
    {
        NodeId = nodeId;
    }

    public uint NodeId { get; }

    public ulong Serial { get; init; }

    public uint ClientId { get; init; }

    public int VerifiedPid { get; init; }

    public int ReportedPid { get; init; }

    public string? FlatpakAppId { get; init; }

    public string? ApplicationName { get; init; }

    public string? MediaRole { get; init; }

    public bool ViaPulse { get; init; }

    public long LaunchId { get; init; }

    public string Name => ApplicationName is { Length: > 0 } name ? name : $"node{NodeId}";
}
