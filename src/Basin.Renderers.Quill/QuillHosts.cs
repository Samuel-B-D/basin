using Basin.Capabilities;
using Basin.Render.Gl;
using Basin.Render.Skia;
using Basin.Render.Vulkan;
using Basin.UI.Quill;

namespace Basin.Renderers;

public static class QuillHosts
{
    public static IUIHost? For(
        IRenderer renderer,
        IAllocator? allocator,
        string? renderNodePath,
        out string? declined,
        out string? vulkanDeclined)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        vulkanDeclined = null;
        if (renderer.Device is VulkanDevice vulkan)
        {
            var host = QuillVulkanUIHost.TryCreate(
                vulkan, vulkan.DevicePath, renderer.DmabufTextureFormats, out vulkanDeclined, allocator: allocator);
            if (host is not null)
            {
                declined = null;
                return host;
            }
        }

        var shared = renderer switch
        {
            GlRenderer gl => gl.Device,
            SkiaGlRenderer skia => skia.Device,
            _ => null,
        };
        return QuillUIHost.TryCreate(
            shared,
            renderer.Device?.DevicePath ?? renderNodePath,
            renderer.DmabufTextureFormats,
            out declined,
            allocator: shared is null ? null : allocator);
    }
}
