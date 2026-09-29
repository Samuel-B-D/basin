using Basin.Scene;
using Xunit;

namespace Basin.Tests;

public sealed class DevicePixelBufferTests
{
    private const int Width = 21;
    private const int Height = 11;

    private static MemoryBuffer Art()
    {
        var art = new MemoryBuffer(Width + 3, Height + 2, DrmFormat.Xrgb8888);
        Assert.True(art.BeginDataAccess(BufferDataAccess.Write, out var view));
        try
        {
            unsafe
            {
                for (var y = 0; y < art.Height; y++)
                {
                    var row = (uint*)((byte*)view.Data + (y * view.Stride));
                    for (var x = 0; x < art.Width; x++)
                    {
                        row[x] = x == 0 ? 0xffff0000u : ((x + y) & 1) == 0 ? 0xff000000u : 0xffffffffu;
                    }
                }
            }
        }
        finally
        {
            art.EndDataAccess();
        }

        return art;
    }

    [Fact]
    public void A_nudge_reaches_a_device_column_no_integer_position_rounds_to()
    {
        using var host = new CompositorTestHost(96, 64);
        using (var state = new OutputState())
        {
            Assert.True(host.Output.Commit(state.SetScale(1.7)));
        }

        var art = Art();
        var node = new SceneBuffer(host.Scene.Root);
        node.SetBuffer(art);
        var target = 4;
        var logical = (int)Math.Floor(target / 1.7);
        node.SetPosition(logical, logical);
        node.SourceBox = new FBox(0, 0, Width, Height);
        node.DestinationWidth = (int)Math.Ceiling(Width / 1.7) + 1;
        node.DestinationHeight = (int)Math.Ceiling(Height / 1.7) + 1;
        node.DevicePixels = (Width, Height);
        node.DeviceScale = 1.7;
        node.DeviceNudge = (target - (int)Math.Round(logical * 1.7), target - (int)Math.Round(logical * 1.7));

        host.RenderFrame();
        Assert.Equal(0xffff0000u, host.Pixel(target, target));
        Assert.Equal(0xff000000u, host.Pixel(target - 1, target));
        node.Destroy();
        art.Destroy();
    }

    [Theory]
    [InlineData(1.7, 7, 5)]
    [InlineData(1.5, 3, 9)]
    [InlineData(1.25, 11, 2)]
    public void A_device_pixel_buffer_lands_one_to_one_at_a_fractional_scale(double scale, int x, int y)
    {
        using var host = new CompositorTestHost(96, 64);
        using (var state = new OutputState())
        {
            Assert.True(host.Output.Commit(state.SetScale(scale)));
        }

        var art = Art();
        var node = new SceneBuffer(host.Scene.Root);
        node.SetBuffer(art);
        node.SetPosition(x, y);
        node.SourceBox = new FBox(0, 0, Width, Height);
        node.DestinationWidth = (int)Math.Ceiling(Width / scale);
        node.DestinationHeight = (int)Math.Ceiling(Height / scale);
        node.DevicePixels = (Width, Height);
        node.DeviceScale = scale;

        host.RenderFrame();
        var left = (int)Math.Round(x * scale);
        var top = (int)Math.Round(y * scale);
        for (var row = 0; row < Height; row++)
        {
            for (var column = 0; column < Width; column++)
            {
                var expected = column == 0 ? 0xffff0000u : ((column + row) & 1) == 0 ? 0xff000000u : 0xffffffffu;
                Assert.Equal(expected, host.Pixel(left + column, top + row));
            }
        }

        Assert.Equal(0xff000000u, host.Pixel(left + Width, top + 1));
        Assert.Equal(0xff000000u, host.Pixel(left + 1, top + Height));

        using var output = new SceneOutput(host.Scene, host.Output);
        using var swapchain = new Swapchain(new ShmAllocator(), 96, 64, DrmFormat.Xrgb8888, [DrmFormatSet.ModifierLinear]);
        using var commit = new OutputState();
        Assert.True(output.Commit(host.Renderer, swapchain, commit, new SceneCommitOptions { AllowDirectScanout = false }));
        var tracked = output.LastTarget!;
        Assert.True(tracked.BeginDataAccess(BufferDataAccess.Read, out var view));
        try
        {
            unsafe
            {
                for (var row = 0; row < host.Target.Height; row++)
                {
                    var line = (uint*)((byte*)view.Data + (row * view.Stride));
                    for (var column = 0; column < host.Target.Width; column++)
                    {
                        Assert.Equal(host.Pixel(column, row), line[column] | 0xff000000u);
                    }
                }
            }
        }
        finally
        {
            tracked.EndDataAccess();
        }

        node.Destroy();
        art.Destroy();
    }

    [Fact]
    public void On_an_output_of_another_scale_the_buffer_fills_its_logical_box()
    {
        using var host = new CompositorTestHost(96, 64);
        var art = Art();
        var node = new SceneBuffer(host.Scene.Root);
        node.SetBuffer(art);
        node.SetPosition(4, 6);
        node.SourceBox = new FBox(0, 0, Width, Height);
        node.DestinationWidth = 11;
        node.DestinationHeight = 6;
        node.DevicePixels = (Width, Height);
        node.DeviceScale = 2;

        host.RenderFrame();
        Assert.True((host.Pixel(4, 6) >> 16 & 0xff) > 0x80, "the art's first column is drawn at the logical origin");
        Assert.NotEqual(0xff000000u, host.Pixel(4 + 10, 6 + 5));
        for (var column = 4 + 11; column < 4 + Width; column++)
        {
            Assert.Equal(0xff000000u, host.Pixel(column, 7));
        }

        Assert.Equal(0xff000000u, host.Pixel(5, 6 + 6));

        using var output = new SceneOutput(host.Scene, host.Output);
        using var swapchain = new Swapchain(new ShmAllocator(), 96, 64, DrmFormat.Xrgb8888, [DrmFormatSet.ModifierLinear]);
        using var commit = new OutputState();
        Assert.True(output.Commit(host.Renderer, swapchain, commit, new SceneCommitOptions { AllowDirectScanout = false }));
        var tracked = output.LastTarget!;
        Assert.True(tracked.BeginDataAccess(BufferDataAccess.Read, out var view));
        try
        {
            unsafe
            {
                var line = (uint*)((byte*)view.Data + (7 * view.Stride));
                for (var column = 4 + 11; column < 4 + Width; column++)
                {
                    Assert.Equal(0xff000000u, line[column] | 0xff000000u);
                }

                Assert.Equal(host.Pixel(4 + 5, 7), line[4 + 5] | 0xff000000u);
            }
        }
        finally
        {
            tracked.EndDataAccess();
        }

        node.Destroy();
        art.Destroy();
    }
}
