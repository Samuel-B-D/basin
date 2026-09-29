using Basin.Capabilities;
using Basin.Desktop;
using Basin.Scene;
using Xunit;

namespace Basin.Tests;

public sealed class WorldSeamTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Toplevel_capture_renders_a_window_under_a_disabled_layer(bool clientOnly)
    {
        using var host = new CompositorTestHost();
        var model = new TestToplevelModel();
        var index = new ToplevelSceneIndex();
        var capture = new SceneScreenCapture(host.Scene, host.Layout)
        {
            Renderer = host.Renderer,
            Toplevels = model,
            Index = index,
        };

        var window = MappedToplevel.Map(host, host.Client, width: 60, height: 50, color: 0xFF00FF00);
        foreach (var stray in host.SurfaceScenes.ToArray())
        {
            stray.Destroy();
        }

        host.SurfaceScenes.Clear();
        var layer = new SceneTree(host.Scene.Root);
        var tree = new SceneTree(layer);
        var scene = new SceneSurface(tree, window.ServerToplevel.Surface);
        layer.Enabled = false;

        var toplevelId = model.Add(string.Empty, "basin-test", window.ServerToplevel.Surface, new Box(0, 0, 60, 50));
        index.Set(toplevelId, new ToplevelCaptureTrees(tree, null, tree));

        var source = CaptureSource.Toplevel(toplevelId, clientOnly);
        Assert.True(capture.TryDescribe(source, out var format));
        Assert.Equal((60, 50), (format.Width, format.Height));

        var target = new MemoryBuffer(60, 50, DrmFormat.Xrgb8888);
        try
        {
            Assert.True(capture.Capture(source, new Box(0, 0, 60, 50), target));
            Assert.True(target.BeginDataAccess(BufferDataAccess.Read, out var view));
            unsafe
            {
                var pixel = *(uint*)(view.Data + (10 * view.Stride) + (10 * 4));
                Assert.Equal(0xFF00FF00u, pixel | 0xFF000000u);
            }

            target.EndDataAccess();
        }
        finally
        {
            target.Destroy();
        }

        scene.Destroy();
        tree.Destroy();
        layer.Destroy();
    }

    [Fact]
    public void A_consumer_surface_mapper_survives_the_scene_seat_input()
    {
        using var host = new CompositorTestHost();
        Basin.Seat.SurfaceMapper mapper = Map;
        host.Seat.Pointer.MapToSurface = mapper;

        _ = new SceneSeatInput(host.Seat, host.Scene, host.Layout);

        Assert.Same(mapper, host.Seat.Pointer.MapToSurface);
        host.Seat.Pointer.MapToSurface = null;

        static bool Map(Surface surface, double x, double y, out double surfaceX, out double surfaceY)
        {
            _ = surface;
            surfaceX = x;
            surfaceY = y;
            return true;
        }
    }
}

public sealed class HiddenSceneBufferTests
{
    [Fact]
    public void A_hidden_node_reuses_the_scene_texture_for_a_buffer_it_returns_to()
    {
        using var host = new CompositorTestHost();
        var layer = new SceneTree(host.Scene.Root) { Enabled = false };
        var node = new SceneBuffer(layer);
        var first = new MemoryBuffer(16, 16, DrmFormat.Argb8888);
        var second = new MemoryBuffer(16, 16, DrmFormat.Argb8888);

        node.SetBuffer(first);
        var version = node.ContentVersion;
        var a = node.TextureFor(host.Renderer);
        Assert.NotNull(a);
        Assert.Same(a, node.TextureFor(host.Renderer));
        node.SetBuffer(second);
        Assert.True(node.ContentVersion > version);
        var b = node.TextureFor(host.Renderer);
        Assert.NotSame(a, b);
        node.SetBuffer(first);
        Assert.Same(a, node.TextureFor(host.Renderer));
        Assert.Equal((16, 16), node.DisplaySize);

        node.Destroy();
        layer.Destroy();
        first.Destroy();
        second.Destroy();
    }
}
