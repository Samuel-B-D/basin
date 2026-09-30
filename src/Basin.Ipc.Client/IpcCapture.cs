using System.Buffers.Binary;
using Microsoft.Win32.SafeHandles;

namespace Basin.Ipc;

public sealed record IpcCapture(
    int Width, int Height, string? Path, byte[]? Png, SafeFileHandle? Fd, string? Format, int Stride)
{
    public byte[] ReadRgba()
    {
        if (Fd is not { IsInvalid: false } fd)
        {
            throw new InvalidOperationException("the capture carries no descriptor; ask for IpcCaptureTarget.Fd");
        }

        if (Width <= 0 || Height <= 0 || Stride < Width * 4)
        {
            throw new InvalidDataException($"a {Width}x{Height} capture with stride {Stride} cannot be read as 32-bit pixels");
        }

        var pixels = new byte[Stride * Height];
        var read = 0;
        while (read < pixels.Length)
        {
            var got = RandomAccess.Read(fd, pixels.AsSpan(read), read);
            if (got <= 0)
            {
                throw new EndOfStreamException($"the capture descriptor ended after {read} of {pixels.Length} bytes");
            }

            read += got;
        }

        var opaque = Format?.StartsWith('x') == true;
        var rgba = new byte[Width * Height * 4];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var pixel = BinaryPrimitives.ReadUInt32LittleEndian(pixels.AsSpan((y * Stride) + (x * 4)));
                var i = ((y * Width) + x) * 4;
                rgba[i] = (byte)(pixel >> 16);
                rgba[i + 1] = (byte)(pixel >> 8);
                rgba[i + 2] = (byte)pixel;
                rgba[i + 3] = opaque ? (byte)0xFF : (byte)(pixel >> 24);
            }
        }

        return rgba;
    }
}
