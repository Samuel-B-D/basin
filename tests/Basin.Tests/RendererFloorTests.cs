using Basin.Renderers;
using Xunit;

namespace Basin.Tests;

public sealed class RendererFloorTests
{
    [Fact]
    public void A_gl_floor_throws_where_the_default_falls_to_pixman()
    {
        var name = "gl";
        var falls = new List<RendererFallback>();
        var stack = RendererCatalog.CreateWithFallback(ref name, "/dev/dri/no-such-node", falls.Add);
        Assert.Equal("pixman", name);
        Release(stack);

        name = "gl";
        Assert.ThrowsAny<Exception>(() => RendererCatalog.CreateWithFallback(ref name, "/dev/dri/no-such-node", floor: "gl"));
    }

    [Fact]
    public void Pixman_has_no_world_renderer()
    {
        var name = "pixman";
        var stack = RendererCatalog.CreateWithFallback(ref name, null);
        Assert.Null(WorldRenderers.For(stack.Renderer, 8, out var declined));
        Assert.NotNull(declined);
        Release(stack);
    }

    private static void Release(RenderStack stack)
    {
        stack.DeviceAllocator?.Dispose();
        stack.Renderer.Dispose();
    }
}
