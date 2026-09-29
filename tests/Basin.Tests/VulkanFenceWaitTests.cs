using Basin.Render.Vulkan;
using Xunit;

namespace Basin.Tests;

public sealed class VulkanFenceWaitTests
{
    [Fact]
    public void Back_to_back_waits_never_reuse_a_semaphore_still_in_flight()
    {
        CompositorTestHost.SkipUnlessRunnable("vulkan");
        using var host = new CompositorTestHost(renderer: "vulkan");
        var device = ((VulkanRenderer)host.Renderer).Device;
        Assert.SkipUnless(device.FenceWait.IsGpuSide, "no external semaphore import on this device");

        for (var i = 0; i < 6; i++)
        {
            var fence = device.ExportQueueFence();
            Assert.True(fence >= 0);
            device.FenceWait.Wait(fence);
            RenderFences.CloseFence(fence);
        }

        var grown = device.FenceWait.Slots;
        Assert.InRange(grown, 1, 6);
        device.WaitFor(device.Submit(device.BeginCommands()));
        _ = device.Api.QueueWaitIdle(device.Queue);

        var fenceAgain = device.ExportQueueFence();
        device.FenceWait.Wait(fenceAgain);
        RenderFences.CloseFence(fenceAgain);
        Assert.Equal(grown, device.FenceWait.Slots);
    }
}
