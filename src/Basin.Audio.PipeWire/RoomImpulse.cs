using System.Globalization;

namespace Basin.Audio.PipeWire;

public static class RoomImpulse
{
    public const int Rate = 48000;

    public const float SecondsQuantum = 0.05f;

    public static string PathFor(float rt60Seconds, float preDelaySeconds = 0.012f, float crossoverHz = 3000, float highDecayRatio = 0.5f, string? directory = null)
    {
        var rt60 = MathF.Max(SecondsQuantum, MathF.Round(rt60Seconds / SecondsQuantum) * SecondsQuantum);
        var preDelay = MathF.Round(Math.Clamp(preDelaySeconds, 0, 0.2f) * 1000) / 1000;
        var crossover = MathF.Round(Math.Clamp(crossoverHz, 200, 16000));
        var ratio = MathF.Round(Math.Clamp(highDecayRatio, 0.1f, 1), 2);
        directory ??= DefaultDirectory();
        var name = string.Create(CultureInfo.InvariantCulture, $"room-v1-rt{rt60:F2}-pd{preDelay:F3}-x{crossover:F0}-h{ratio:F2}.wav");
        var path = Path.Combine(directory, name);
        if (File.Exists(path))
        {
            return path;
        }

        Directory.CreateDirectory(directory);
        var (left, right) = Generate(rt60, preDelay, crossover, ratio);
        var temporary = path + $".{Environment.ProcessId}.tmp";
        Write(temporary, left, right);
        File.Move(temporary, path, overwrite: true);
        return path;
    }

    public static (float[] Left, float[] Right) Generate(float rt60Seconds, float preDelaySeconds, float crossoverHz, float highDecayRatio)
    {
        var length = (int)((preDelaySeconds + (rt60Seconds * 1.1f)) * Rate) + 1;
        var delay = (int)(preDelaySeconds * Rate);
        var left = new float[length];
        var right = new float[length];
        var seed = BitConverter.SingleToInt32Bits(rt60Seconds) ^ (BitConverter.SingleToInt32Bits(preDelaySeconds) * 31) ^
            (BitConverter.SingleToInt32Bits(crossoverHz) * 17) ^ (BitConverter.SingleToInt32Bits(highDecayRatio) * 7);
        Fill(left, delay, rt60Seconds, crossoverHz, highDecayRatio, new Random(seed));
        Fill(right, delay, rt60Seconds, crossoverHz, highDecayRatio, new Random(seed ^ 0x5bd1e995));
        Normalize(left, right);
        return (left, right);
    }

    private static void Fill(float[] samples, int delay, float rt60, float crossover, float highRatio, Random random)
    {
        var lowDecay = MathF.Log(1000) / (rt60 * Rate);
        var highDecay = MathF.Log(1000) / (rt60 * highRatio * Rate);
        var alpha = 1 - MathF.Exp(-2 * MathF.PI * crossover / Rate);
        float a = 0, b = 0, c = 0, low = 0;
        for (var i = delay; i < samples.Length; i++)
        {
            var noise = ((float)random.NextDouble() * 2) - 1;
            a += alpha * (noise - a);
            b += alpha * (a - b);
            c += alpha * (b - c);
            low += alpha * (c - low);
            var high = noise - low;
            var t = i - delay;
            samples[i] = (low * MathF.Exp(-lowDecay * t)) + (high * MathF.Exp(-highDecay * t));
        }
    }

    private static void Normalize(float[] left, float[] right)
    {
        double energy = 0;
        for (var i = 0; i < left.Length; i++)
        {
            energy += (left[i] * (double)left[i]) + (right[i] * (double)right[i]);
        }

        var scale = energy > 0 ? (float)(1 / Math.Sqrt(energy / 2)) : 0;
        for (var i = 0; i < left.Length; i++)
        {
            left[i] *= scale;
            right[i] *= scale;
        }
    }

    private static void Write(string path, float[] left, float[] right)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + (left.Length * 8));
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)3);
        writer.Write((short)2);
        writer.Write(Rate);
        writer.Write(Rate * 8);
        writer.Write((short)8);
        writer.Write((short)32);
        writer.Write("data"u8);
        writer.Write(left.Length * 8);
        for (var i = 0; i < left.Length; i++)
        {
            writer.Write(left[i]);
            writer.Write(right[i]);
        }
    }

    private static string DefaultDirectory()
    {
        var runtime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        return Path.Combine(runtime is { Length: > 0 } ? runtime : Path.GetTempPath(), "basin-audio");
    }
}
