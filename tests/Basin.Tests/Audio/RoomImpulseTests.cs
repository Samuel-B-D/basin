using Basin.Audio.PipeWire;
using Xunit;

namespace Basin.Tests;

public sealed class RoomImpulseTests
{
    [Fact]
    public void An_impulse_is_a_stereo_float_wav_that_falls_sixty_decibels_by_its_reverb_time()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"basin-ir-{Guid.NewGuid():N}");
        try
        {
            var path = RoomImpulse.PathFor(0.8f, directory: directory);
            var (left, right) = AudioSignal.ReadWav(path);

            Assert.Equal(left.Length, right.Length);
            Assert.InRange(left.Length, (int)(0.8 * RoomImpulse.Rate), (int)(1.0 * RoomImpulse.Rate));
            var delay = (int)(0.012 * RoomImpulse.Rate);
            Assert.All(left[..delay], sample => Assert.Equal(0, sample));
            var start = AudioSignal.RmsDb(left.AsSpan(delay, 2400));
            var end = AudioSignal.RmsDb(left.AsSpan(delay + (int)(0.78 * RoomImpulse.Rate), 2400));
            Assert.InRange(start - end, 50, 75);
            Assert.True(Correlation(left, right) < 0.2, "the channels are not decorrelated");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Impulses_are_cached_by_their_quantized_parameters()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"basin-ir-{Guid.NewGuid():N}");
        try
        {
            var first = RoomImpulse.PathFor(0.61f, directory: directory);
            var written = File.GetLastWriteTimeUtc(first);

            Assert.Equal(first, RoomImpulse.PathFor(0.59f, directory: directory));
            Assert.Equal(written, File.GetLastWriteTimeUtc(first));
            Assert.NotEqual(first, RoomImpulse.PathFor(0.7f, directory: directory));
            Assert.Single(Directory.GetFiles(directory, "*0.60*"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void The_high_band_dies_faster_than_the_low_band()
    {
        var (left, _) = RoomImpulse.Generate(1.0f, 0, 3000, 0.5f);
        var late = left.AsSpan((int)(0.4 * RoomImpulse.Rate), 4800);
        var early = left.AsSpan(0, 4800);

        var lowLoss = LowBandDb(early, 800) - LowBandDb(late, 800);
        var highLoss = AudioSignal.HighBandDb(early, 8000) - AudioSignal.HighBandDb(late, 8000);
        Assert.True(highLoss > lowLoss + 10, $"high band fell {highLoss:F1} dB, low band {lowLoss:F1} dB");
    }

    private static double LowBandDb(ReadOnlySpan<float> samples, double below)
    {
        var filtered = samples.ToArray();
        var alpha = (float)(1 - Math.Exp(-2 * Math.PI * below / RoomImpulse.Rate));
        for (var pass = 0; pass < 4; pass++)
        {
            var state = 0f;
            for (var i = 0; i < filtered.Length; i++)
            {
                state += alpha * (filtered[i] - state);
                filtered[i] = state;
            }
        }

        return AudioSignal.RmsDb(filtered);
    }

    private static double Correlation(float[] a, float[] b)
    {
        double ab = 0, aa = 0, bb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            ab += a[i] * (double)b[i];
            aa += a[i] * (double)a[i];
            bb += b[i] * (double)b[i];
        }

        return Math.Abs(ab / Math.Sqrt(aa * bb));
    }
}
