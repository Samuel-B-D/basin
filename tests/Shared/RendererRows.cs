using Basin.Renderers;

namespace Basin.Tests;

internal static class RendererRows
{
    public static bool NeedsVulkan(string renderer) => renderer is "vulkan" or "skia-vulkan" or "skia-graphite";

    public static bool IsRunnable(string renderer) =>
        RendererCatalog.Names.Contains(renderer)
        && (!RendererCatalog.NeedsGpu(renderer) || File.Exists(RenderNode.Path))
        && (!NeedsVulkan(renderer) || VulkanRunnability.Runnable);
}
