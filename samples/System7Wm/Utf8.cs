namespace System7Wm;

internal static class Utf8
{
    public static int CompleteLength(ReadOnlySpan<byte> data)
    {
        var end = data.Length;
        for (var back = 1; back <= Math.Min(3, end); back++)
        {
            var b = data[end - back];
            if ((b & 0xC0) == 0x80)
            {
                continue;
            }

            var need = b >= 0xF0 ? 4 : b >= 0xE0 ? 3 : b >= 0xC0 ? 2 : 1;
            return need > back ? end - back : end;
        }

        return end;
    }
}
