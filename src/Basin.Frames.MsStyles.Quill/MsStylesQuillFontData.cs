using System.Buffers.Binary;

namespace Basin.Frames.MsStyles.Quill;

internal static class MsStylesQuillFontData
{
    public static byte[]? Standalone(byte[] data, int index)
    {
        if (data.Length < 12)
        {
            return null;
        }

        if (BinaryPrimitives.ReadUInt32BigEndian(data) != 0x74746366)
        {
            return data;
        }

        var count = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(8));
        if (index < 0 || index >= count || 12 + (4 * (index + 1)) > data.Length)
        {
            return null;
        }

        var start = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(12 + (4 * index)));
        if (start < 0 || start + 12 > data.Length)
        {
            return null;
        }

        var tables = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(start + 4));
        var directory = 12 + (16 * tables);
        if (start + directory > data.Length)
        {
            return null;
        }

        var size = directory;
        for (var i = 0; i < tables; i++)
        {
            var record = data.AsSpan(start + 12 + (16 * i));
            var offset = BinaryPrimitives.ReadUInt32BigEndian(record[8..]);
            var length = BinaryPrimitives.ReadUInt32BigEndian(record[12..]);
            if (offset + (long)length > data.Length)
            {
                return null;
            }

            size += (int)((length + 3) & ~3u);
        }

        var font = new byte[size];
        data.AsSpan(start, directory).CopyTo(font);
        var at = directory;
        for (var i = 0; i < tables; i++)
        {
            var record = font.AsSpan(12 + (16 * i));
            var offset = (int)BinaryPrimitives.ReadUInt32BigEndian(record[8..]);
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(record[12..]);
            data.AsSpan(offset, length).CopyTo(font.AsSpan(at));
            BinaryPrimitives.WriteUInt32BigEndian(record[8..], (uint)at);
            at += (length + 3) & ~3;
        }

        return font;
    }
}
