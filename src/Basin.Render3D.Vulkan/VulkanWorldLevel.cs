using Basin.Scene3D;

namespace Basin.Render3D.Vulkan;

internal sealed class VulkanWorldLevel
{
    public WorldView View;

    public WorldVisibleSet OwnSet { get; } = new();

    public WorldVisibleSet Set { get; set; } = null!;

    public int Depth { get; set; }

    public List<(WorldPortal Portal, int Child)> Portals { get; } = [];

    public VulkanWorldBuffer? SceneBuffer { get; set; }

    public ulong SceneOffset { get; set; }
}
