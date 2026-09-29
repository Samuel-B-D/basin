namespace Basin.Render3D.Vulkan;

internal readonly record struct VulkanWorldSetKey(ulong View, ulong Sampler, ulong Draw, ulong Scene, ulong Joints);
