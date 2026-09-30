using Basin.Render.Gl;
using Basin.Render.Vulkan;
using Basin.Render3D.Gl;
using Basin.Render3D.Vulkan;
using Basin.Scene3D;

namespace Basin.Renderers;

public static class WorldRenderers
{
    public static IWorldRenderer? For(IRenderer renderer, float anisotropy, out string? declined)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        declined = null;
        switch (renderer)
        {
            case VulkanRenderer vulkan:
                return new VulkanWorldRenderer(vulkan.Device, anisotropy);
            case GlRenderer gl:
                return new GlWorldRenderer(gl.Device);
            default:
                declined = "its textures are not GL or Vulkan textures, so the 3D pass cannot open them";
                return null;
        }
    }
}
