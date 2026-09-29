using Silk.NET.Vulkan;

namespace Basin.Render.Vulkan;

public static class VulkanTextures
{
    public static bool TryOpen(VulkanDevice device, ITexture texture, CommandBuffer uploads, out VulkanTextureView view)
    {
        ArgumentNullException.ThrowIfNull(device);
        switch (texture)
        {
            case VulkanDmabufTexture dmabuf when ReferenceEquals(dmabuf.Device, device) && dmabuf.Ycbcr is null:
                view = new VulkanTextureView(
                    dmabuf.Image, dmabuf.View, dmabuf.VkFormat, ImageLayout.General,
                    dmabuf.Width, dmabuf.Height, true, dmabuf.HasAlpha, true);
                return true;
            case VulkanShmTexture shm when ReferenceEquals(shm.Device, device):
                if (!shm.PrepareUpload(device.Staging))
                {
                    break;
                }

                if (shm.NeedsGpuCopy)
                {
                    shm.RecordGpuCopy(uploads);
                }

                view = new VulkanTextureView(
                    shm.Image, shm.View, shm.VkFormat, ImageLayout.General,
                    shm.Width, shm.Height, true, shm.HasAlpha, false);
                return true;
        }

        view = default;
        return false;
    }

    public static bool TryGetDmabuf(ITexture texture, out DmabufAttributes attributes)
    {
        if (texture is VulkanDmabufTexture dmabuf)
        {
            attributes = dmabuf.Attributes;
            return true;
        }

        attributes = default;
        return false;
    }
}
