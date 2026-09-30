using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Basin.Ipc;
using Xunit;

namespace Basin.Tests;

public sealed class IpcClientHelperTests
{
    [Fact]
    public void An_event_matches_on_equal_keys_substrings_and_numbers_given_as_text()
    {
        var data = Encoding.UTF8.GetBytes("""{"app_id":"org.example.Foot","id":42,"focused":true}""");
        using var exact = JsonDocument.Parse("""{"app_id":"org.example.Foot","focused":true}""");
        using var substring = JsonDocument.Parse("""{"app_id":"*Foot*"}""");
        using var number = JsonDocument.Parse("""{"id":"42"}""");
        using var wrong = JsonDocument.Parse("""{"app_id":"Foot"}""");
        using var missing = JsonDocument.Parse("""{"title":"x"}""");

        Assert.True(IpcEventMatch.Matches(data, null));
        Assert.True(IpcEventMatch.Matches(data, exact.RootElement));
        Assert.True(IpcEventMatch.Matches(data, substring.RootElement));
        Assert.True(IpcEventMatch.Matches(data, number.RootElement));
        Assert.False(IpcEventMatch.Matches(data, wrong.RootElement));
        Assert.False(IpcEventMatch.Matches(data, missing.RootElement));
    }

    [Fact]
    public void A_capture_descriptor_reads_back_as_rgba_across_a_padded_stride()
    {
        var path = Path.Combine(Path.GetTempPath(), "basin-capture-" + Guid.NewGuid().ToString("N"));
        var bytes = new byte[16 * 2];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0), 0x80112233u);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0xff445566u);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 0x00778899u);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(20), 0x10aabbccu);
        File.WriteAllBytes(path, bytes);
        try
        {
            using (var handle = File.OpenHandle(path))
            {
                var argb = new IpcCapture(2, 2, null, null, handle, "argb8888", 16).ReadRgba();
                Assert.Equal(new byte[] { 0x11, 0x22, 0x33, 0x80, 0x44, 0x55, 0x66, 0xff, 0x77, 0x88, 0x99, 0x00, 0xaa, 0xbb, 0xcc, 0x10 }, argb);
            }

            using (var handle = File.OpenHandle(path))
            {
                var xrgb = new IpcCapture(2, 2, null, null, handle, "xrgb8888", 16).ReadRgba();
                Assert.Equal(0xff, xrgb[11]);
            }

            Assert.Throws<InvalidOperationException>(() => new IpcCapture(2, 2, null, null, null, "argb8888", 16).ReadRgba());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
