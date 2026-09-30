using Basin.Transport.Waypipe;
using Wayland.Server;
using Xunit;

namespace Basin.Tests;

public sealed class WaypipeAdoptionTests
{
    [Fact]
    public void The_filter_holds_a_channel_client_to_what_it_carries_and_leaves_a_local_one()
    {
        using var display = WlServerDisplay.Create(new ManagedTransport());
        var buffers = new ClientBufferRegistry();
        using var compositor = new CompositorGlobal(display, buffers);
        using var remote = new LoopbackChannel(WaypipeCompression.None, new WaypipeChannelOptions { CarriesDmabuf = true });
        using var local = new LoopbackChannel(WaypipeCompression.None);
        using var adoption = new WaypipeAdoption(
            display,
            buffers,
            compositor,
            hostHasDmabuf: false,
            inner: (_, _, name) => name != "ext_session_lock_manager_v1");

        var adopted = adoption.Adopt(remote.Channel);
        var plain = display.CreateClient(local.Channel.Transport);

        Assert.Equal(1, adoption.Count);
        Assert.True(adoption.Visible(adopted, null, "zwp_linux_dmabuf_v1"));
        Assert.False(adoption.Visible(plain, null, "zwp_linux_dmabuf_v1"));
        Assert.False(adoption.Visible(adopted, null, "wp_linux_drm_syncobj_manager_v1"));
        Assert.True(adoption.Visible(plain, null, "wp_linux_drm_syncobj_manager_v1"));
        Assert.False(adoption.Visible(plain, null, "ext_session_lock_manager_v1"));

        adopted.Destroy();
        Assert.Equal(0, adoption.Count);
        plain.Destroy();
    }
}
