using Basin.Backend.Wayland;
using Xunit;

namespace Basin.Tests;

public class NestedOutputAllocationTests
{
    [Fact]
    public void A_software_frame_to_a_parent_that_offers_dmabuf_allocates_nothing()
    {
        var formats = new DrmFormatSet();
        formats.Add(DrmFormat.Xrgb8888, 0);
        using var host = new NestedBackendTestHost(NestedParentOptions.Undecorating with
        {
            Dmabuf = true,
            DmabufFormats = formats,
            AnswersFrames = true,
        });

        var output = host.CreateOutput();
        host.Pump();
        var buffers = new[]
        {
            new MemoryBuffer(output.CurrentMode.Width, output.CurrentMode.Height, DrmFormat.Xrgb8888),
            new MemoryBuffer(output.CurrentMode.Width, output.CurrentMode.Height, DrmFormat.Xrgb8888),
        };
        using var state = new OutputState();

        long Measure(int frames)
        {
            long total = 0;
            for (var i = 0; i < frames; i++)
            {
                state.SetBuffer(buffers[i % 2]);
                var before = GC.GetAllocatedBytesForCurrentThread();
                var committed = output.Commit(state);
                total += GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.True(committed, $"frame {i} committed");
                host.Pump();
            }

            return total;
        }

        Measure(20);
        var first = Measure(100);
        var second = first == 0 ? 0 : Measure(100);
        Assert.True(second == 0, $"a nested software commit allocated {first} bytes over 100 frames, then {second} over the next 100");
    }
}
