using Basin.Audio.PipeWire;
using Xunit;

namespace Basin.Tests;

[Collection(AudioDaemonCollection.Name)]
public sealed class SpatialVoiceAllocationTests(AudioDaemonFixture daemon)
{
    private const int Rounds = 1000;

    [Theory]
    [InlineData(VoiceMode.Pan, "voice-update-pan")]
    [InlineData(VoiceMode.Hrtf, "voice-update-hrtf")]
    [InlineData(VoiceMode.Pan, "voice-update-reverb")]
    public void Changing_updates_allocate_nothing(VoiceMode mode, string path)
    {
        daemon.SkipIfUnavailable();
        Assert.SkipWhen(mode == VoiceMode.Hrtf && !File.Exists(Sofa), $"{Sofa} is not installed");
        using var rig = new AudioRig(daemon);
        var graph = path.EndsWith("reverb", StringComparison.Ordinal) ? Graph(mode) with { ImpulseFile = RoomImpulse.PathFor(0.5f, directory: rig.Directory) } : Graph(mode);
        using var voice = SpatialVoice.Create(rig.Session, graph, $"basin-test-{path}", out var error);
        Assert.True(voice is not null, error);
        voice.MinHrtfInterval = TimeSpan.Zero;

        var step = 0;
        long Measure()
        {
            long total = 0;
            for (var i = 0; i < Rounds; i++)
            {
                step++;
                var wanted = new VoiceParams(step * 3 % 360, (step % 7) * 5 - 15, 1 + (step % 5), -(step % 40) * 0.5f, 500 + (step % 30) * 600, -(step % 20));
                var before = GC.GetAllocatedBytesForCurrentThread();
                voice.Update(wanted);
                total += GC.GetAllocatedBytesForCurrentThread() - before;
                if (i % 64 == 0)
                {
                    rig.Session.Pump(0);
                }
            }

            return total;
        }

        _ = Measure();
        var writes = voice.ParamWrites;
        var measured = Measure();

        Assert.True(voice.ParamWrites - writes >= Rounds - 1, $"only {voice.ParamWrites - writes} of {Rounds} updates were sent");
        Assert.Equal(0, measured);
        if (Budgets.Recorded)
        {
            Budgets.Check("audio", path, measured, Path.Combine(Path.GetDirectoryName(Here())!, "..", "Budgets.cs"));
        }
    }

    private const string Sofa = "/usr/share/libmysofa/default.sofa";

    private static VoiceGraph Graph(VoiceMode mode) => mode == VoiceMode.Hrtf ? new(mode, Sofa) : new(mode);

    private static string Here([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;
}
