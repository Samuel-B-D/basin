using Silk.NET.Vulkan;

namespace Basin.Render.Vulkan;

public readonly record struct VulkanTextureView(
    Image Image,
    ImageView View,
    Format Format,
    ImageLayout Layout,
    int Width,
    int Height,
    bool Premultiplied,
    bool HasAlpha,
    bool Foreign);
