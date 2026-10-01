using System.Buffers.Binary;

namespace Basin.Tests;

internal static class AudioSignal
{
    public const int Rate = 48000;

    public static void WriteWav(string path, float[] left, float[] right)
    {
        var frames = left.Length;
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + (frames * 8));
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)3);
        writer.Write((short)2);
        writer.Write(Rate);
        writer.Write(Rate * 8);
        writer.Write((short)8);
        writer.Write((short)32);
        writer.Write("data"u8);
        writer.Write(frames * 8);
        for (var i = 0; i < frames; i++)
        {
            writer.Write(left[i]);
            writer.Write(right[i]);
        }
    }

    public static (float[] Left, float[] Right) Sine(double hertz, double seconds, float amplitude = 0.5f)
    {
        var frames = (int)(seconds * Rate);
        var left = new float[frames];
        for (var i = 0; i < frames; i++)
        {
            left[i] = amplitude * (float)Math.Sin(2 * Math.PI * hertz * i / Rate);
        }

        return (left, (float[])left.Clone());
    }

    public static (float[] Left, float[] Right) Noise(double seconds, float amplitude = 0.3f, int seed = 7)
    {
        var random = new Random(seed);
        var frames = (int)(seconds * Rate);
        var left = new float[frames];
        for (var i = 0; i < frames; i++)
        {
            left[i] = amplitude * ((float)random.NextDouble() * 2 - 1);
        }

        return (left, (float[])left.Clone());
    }

    public static (float[] Left, float[] Right) ReadWav(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var offset = 12;
        short format = 1, channels = 2, bits = 16;
        while (offset + 8 <= bytes.Length)
        {
            var id = System.Text.Encoding.ASCII.GetString(bytes, offset, 4);
            var size = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset + 4));
            if (id == "fmt ")
            {
                format = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset + 8));
                channels = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset + 10));
                bits = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset + 22));
                if (format == unchecked((short)0xFFFE) && size >= 40)
                {
                    format = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset + 32));
                }
            }
            else if (id == "data")
            {
                var start = offset + 8;
                var length = size <= 0 || start + size > bytes.Length ? bytes.Length - start : size;
                return Decode(bytes.AsSpan(start, length), format, channels, bits);
            }

            offset += 8 + Math.Max(0, size) + (size & 1);
        }

        return ([], []);
    }

    public static double RmsDb(ReadOnlySpan<float> samples)
    {
        double sum = 0;
        foreach (var sample in samples)
        {
            sum += sample * (double)sample;
        }

        var rms = samples.Length == 0 ? 0 : Math.Sqrt(sum / samples.Length);
        return 20 * Math.Log10(Math.Max(rms, 1e-9));
    }

    public static double HighBandDb(ReadOnlySpan<float> samples, double above)
    {
        var filtered = new float[samples.Length];
        var alpha = 1 / (1 + (2 * Math.PI * above / Rate));
        float previousIn = 0, previousOut = 0;
        for (var pass = 0; pass < 4; pass++)
        {
            ReadOnlySpan<float> input = pass == 0 ? samples : filtered;
            previousIn = previousOut = 0;
            for (var i = 0; i < input.Length; i++)
            {
                var x = input[i];
                var y = (float)(alpha * (previousOut + x - previousIn));
                previousIn = x;
                previousOut = y;
                filtered[i] = y;
            }
        }

        return RmsDb(filtered);
    }

    public static (float[] Left, float[] Right) Steady((float[] Left, float[] Right) signal, double skipSeconds)
    {
        var skip = Math.Min(signal.Left.Length, (int)(skipSeconds * Rate));
        var end = signal.Left.Length;
        while (end > skip && signal.Left[end - 1] == 0 && signal.Right[end - 1] == 0)
        {
            end--;
        }

        return (signal.Left[skip..end], signal.Right[skip..end]);
    }

    private static (float[] Left, float[] Right) Decode(ReadOnlySpan<byte> data, short format, short channels, short bits)
    {
        var bytesPerSample = bits / 8;
        var frames = data.Length / (bytesPerSample * channels);
        var left = new float[frames];
        var right = new float[frames];
        for (var i = 0; i < frames; i++)
        {
            left[i] = Sample(data[(i * channels * bytesPerSample)..], format, bits);
            right[i] = Sample(data[(((i * channels) + Math.Min(1, channels - 1)) * bytesPerSample)..], format, bits);
        }

        return (left, right);
    }

    private static float Sample(ReadOnlySpan<byte> at, short format, short bits) => (format, bits) switch
    {
        (3, 32) => BinaryPrimitives.ReadSingleLittleEndian(at),
        (_, 16) => BinaryPrimitives.ReadInt16LittleEndian(at) / 32768f,
        (_, 32) => BinaryPrimitives.ReadInt32LittleEndian(at) / 2147483648f,
        _ => 0,
    };
}
