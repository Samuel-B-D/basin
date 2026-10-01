using Basin.Audio.PipeWire;

namespace Basin.Tests;

internal static class SpatialVoiceTestChain
{
    public static SpatialVoice Start(AudioRig rig, VoiceGraph graph, string name, (float[] Left, float[] Right)? source = null)
    {
        var wav = Path.Combine(rig.Directory, $"{name}-source.wav");
        var (left, right) = source ?? AudioSignal.Noise(30);
        AudioSignal.WriteWav(wav, left, right);
        var voice = SpatialVoice.Create(rig.Session, graph, $"basin-test-{name}", out var error) ?? throw new InvalidOperationException(error);
        var output = rig.Node($"basin-test-{name}.output");
        var sink = rig.Node(AudioDaemonFixture.SinkName);
        rig.Play(wav, $"basin-test-{name}-source");
        var player = rig.Node($"basin-test-{name}-source");
        rig.Configure(player.Id, output: true);
        rig.Configure(voice.CaptureNodeId, output: false);
        rig.Configure(output.Id, output: true);
        rig.Configure(sink.Id, output: false, monitor: true);
        rig.Link(player.Id, voice.CaptureNodeId);
        rig.Link(output.Id, sink.Id);
        return voice;
    }
}
