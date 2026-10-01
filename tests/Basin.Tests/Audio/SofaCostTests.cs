using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Basin.Audio.PipeWire;
using Xunit;

namespace Basin.Tests;

[Collection(AudioDaemonCollection.Name)]
public sealed class SofaCostTests(AudioDaemonFixture daemon)
{
    private const string Sofa = "/usr/share/libmysofa/default.sofa";

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(30)]
    public void Measure_the_compositor_thread_time_of_a_turning_hrtf_voice(int updateHz)
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("BASIN_AUDIO_MEASURE") == "1", "a measurement: set BASIN_AUDIO_MEASURE=1 to run it");
        daemon.SkipIfUnavailable();
        Assert.SkipUnless(SofaFile.TryProbe(Sofa, out var min, out var max, out var reason), reason ?? "no SOFA file");
        using var rig = new AudioRig(daemon);
        var graph = updateHz < 0 ? new VoiceGraph(VoiceMode.Pan) : new VoiceGraph(VoiceMode.Hrtf, Sofa) { HrtfRadiusMin = min, HrtfRadiusMax = max };
        using var voice = SpatialVoiceTestChain.Start(rig, graph, $"cost-{updateHz}");
        voice.MinHrtfInterval = updateHz <= 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(1.0 / updateHz);
        rig.Pump(TimeSpan.FromMilliseconds(500));

        const int Frames = 144 * 3;
        var busy = 0L;
        var cpu = ThreadCpu();
        var start = Stopwatch.GetTimestamp();
        for (var frame = 0; frame < Frames; frame++)
        {
            var begin = Stopwatch.GetTimestamp();
            voice.Update(VoiceParams.Centered with { Azimuth = frame * 2.5f % 360 });
            rig.Session.Pump(0);
            rig.Loop.Dispatch(0);
            busy += Stopwatch.GetTimestamp() - begin;
            var next = start + ((frame + 1) * Stopwatch.Frequency / 144);
            while (Stopwatch.GetTimestamp() < next)
            {
                Thread.Sleep(1);
                begin = Stopwatch.GetTimestamp();
                rig.Session.Pump(0);
                rig.Loop.Dispatch(0);
                busy += Stopwatch.GetTimestamp() - begin;
            }
        }

        var seconds = Stopwatch.GetElapsedTime(start).TotalSeconds;
        var thread = (ThreadCpu() - cpu) / 1e6;
        var line = string.Create(CultureInfo.InvariantCulture,
            $"SOFA-COST update_hz={updateHz} frames={Frames} seconds={seconds:F2} graph={graph.Mode} param_writes={voice.ParamWrites} sofa_writes={voice.HrtfWrites} busy_ms={busy * 1000.0 / Stopwatch.Frequency:F1} thread_cpu_ms={thread:F1} per_write_ms={thread / Math.Max(1, voice.HrtfWrites):F3} per_second_ms={thread / seconds:F1}");
        TestContext.Current.SendDiagnosticMessage(line);
        File.AppendAllText(Path.Combine(Path.GetTempPath(), "basin-sofa-cost.txt"), line + "\n");
    }

    private static long ThreadCpu()
    {
        _ = ClockGetTime(3, out var time);
        return (time.Seconds * 1_000_000_000L) + time.Nanoseconds;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TimeSpec
    {
        public long Seconds;
        public long Nanoseconds;
    }

    [DllImport("libc", EntryPoint = "clock_gettime")]
    private static extern int ClockGetTime(int clock, out TimeSpec time);
}
