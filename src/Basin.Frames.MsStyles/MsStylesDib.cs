using System.Buffers.Binary;

namespace Basin.Frames.MsStyles;

internal static class MsStylesDib
{
    private const int DefaultKey = 0xff00ff;

    public static bool TryDecode(ReadOnlySpan<byte> file, out int width, out int height, out byte[] pixels, out bool hasAlpha, out bool hasDefaultKey)
    {
        width = 0;
        height = 0;
        pixels = [];
        hasAlpha = false;
        hasDefaultKey = false;
        if (file.Length < 14 + 12 || file[0] != 'B' || file[1] != 'M')
        {
            return false;
        }

        var offBits = BinaryPrimitives.ReadInt32LittleEndian(file[10..]);
        var dib = file[14..];
        var headerSize = BinaryPrimitives.ReadInt32LittleEndian(dib);
        int bitCount;
        int compression = 0;
        int colorsUsed = 0;
        bool topDown = false;
        if (headerSize == 12)
        {
            width = BinaryPrimitives.ReadUInt16LittleEndian(dib[4..]);
            height = BinaryPrimitives.ReadUInt16LittleEndian(dib[6..]);
            bitCount = BinaryPrimitives.ReadUInt16LittleEndian(dib[10..]);
        }
        else if (headerSize >= 40 && dib.Length >= 40)
        {
            width = BinaryPrimitives.ReadInt32LittleEndian(dib[4..]);
            height = BinaryPrimitives.ReadInt32LittleEndian(dib[8..]);
            bitCount = BinaryPrimitives.ReadUInt16LittleEndian(dib[14..]);
            compression = BinaryPrimitives.ReadInt32LittleEndian(dib[16..]);
            colorsUsed = BinaryPrimitives.ReadInt32LittleEndian(dib[32..]);
            if (height < 0)
            {
                topDown = true;
                height = -height;
            }
        }
        else
        {
            return false;
        }

        if (width <= 0 || height <= 0 || width > 16384 || height > 16384 || compression is not (0 or 3))
        {
            return false;
        }

        uint redMask = 0, greenMask = 0, blueMask = 0;
        var paletteOffset = headerSize;
        if (compression == 3)
        {
            if (headerSize == 40)
            {
                redMask = BinaryPrimitives.ReadUInt32LittleEndian(dib[40..]);
                greenMask = BinaryPrimitives.ReadUInt32LittleEndian(dib[44..]);
                blueMask = BinaryPrimitives.ReadUInt32LittleEndian(dib[48..]);
                paletteOffset += 12;
            }
            else
            {
                redMask = BinaryPrimitives.ReadUInt32LittleEndian(dib[40..]);
                greenMask = BinaryPrimitives.ReadUInt32LittleEndian(dib[44..]);
                blueMask = BinaryPrimitives.ReadUInt32LittleEndian(dib[48..]);
            }
        }
        else if (bitCount == 16)
        {
            redMask = 0x7c00;
            greenMask = 0x03e0;
            blueMask = 0x001f;
        }

        var entrySize = headerSize == 12 ? 3 : 4;
        var paletteCount = bitCount <= 8 ? (colorsUsed != 0 ? colorsUsed : 1 << bitCount) : 0;
        Span<uint> palette = stackalloc uint[256];
        for (var i = 0; i < Math.Min(paletteCount, 256); i++)
        {
            var entry = paletteOffset + i * entrySize;
            if (entry + 3 > dib.Length)
            {
                return false;
            }

            palette[i] = (uint)(dib[entry] | (dib[entry + 1] << 8) | (dib[entry + 2] << 16));
            if (palette[i] == DefaultKey)
            {
                hasDefaultKey = true;
            }
        }

        var stride = ((width * bitCount + 31) / 32) * 4;
        if (offBits < 14 || offBits + (long)stride * height > file.Length)
        {
            return false;
        }

        var bits = file[offBits..];
        pixels = new byte[width * height * 4];
        var anyAlpha = false;
        for (var y = 0; y < height; y++)
        {
            var row = bits.Slice((topDown ? y : height - 1 - y) * stride, stride);
            for (var x = 0; x < width; x++)
            {
                uint rgb;
                byte alpha = 255;
                switch (bitCount)
                {
                    case 1:
                        rgb = palette[(row[x >> 3] >> (7 - (x & 7))) & 1];
                        break;
                    case 4:
                        rgb = palette[(row[x >> 1] >> ((x & 1) == 0 ? 4 : 0)) & 0xf];
                        break;
                    case 8:
                        rgb = palette[row[x]];
                        break;
                    case 16:
                        var value16 = BinaryPrimitives.ReadUInt16LittleEndian(row[(x * 2)..]);
                        rgb = Masked(value16, redMask, greenMask, blueMask);
                        if ((value16 & (redMask | blueMask)) == (redMask | blueMask) && (value16 & greenMask) == 0)
                        {
                            hasDefaultKey = true;
                        }

                        break;
                    case 24:
                        rgb = (uint)(row[x * 3] | (row[x * 3 + 1] << 8) | (row[x * 3 + 2] << 16));
                        if (rgb == DefaultKey)
                        {
                            hasDefaultKey = true;
                        }

                        break;
                    case 32:
                        var value32 = BinaryPrimitives.ReadUInt32LittleEndian(row[(x * 4)..]);
                        if (compression == 3)
                        {
                            rgb = Masked(value32, redMask, greenMask, blueMask);
                        }
                        else
                        {
                            rgb = value32 & 0xffffff;
                            alpha = (byte)(value32 >> 24);
                            anyAlpha |= alpha != 255;
                        }

                        if (rgb == DefaultKey && alpha == 255)
                        {
                            hasDefaultKey = true;
                        }

                        break;
                    default:
                        return false;
                }

                var target = (y * width + x) * 4;
                pixels[target] = (byte)rgb;
                pixels[target + 1] = (byte)(rgb >> 8);
                pixels[target + 2] = (byte)(rgb >> 16);
                pixels[target + 3] = alpha;
            }
        }

        hasAlpha = bitCount == 32 && compression == 0 && anyAlpha;
        if (!hasAlpha)
        {
            for (var i = 3; i < pixels.Length; i += 4)
            {
                pixels[i] = 255;
            }
        }
        else
        {
            MsStylesImageCache.Premultiply(pixels);
        }

        return true;
    }

    private static uint Masked(uint value, uint red, uint green, uint blue) =>
        (Channel(value, blue)) | (Channel(value, green) << 8) | (Channel(value, red) << 16);

    private static uint Channel(uint value, uint mask)
    {
        if (mask == 0)
        {
            return 0;
        }

        var shift = System.Numerics.BitOperations.TrailingZeroCount(mask);
        var bits = System.Numerics.BitOperations.PopCount(mask);
        var raw = (value & mask) >> shift;
        var max = (1u << bits) - 1;
        return (raw * 255 + max / 2) / max;
    }
}
