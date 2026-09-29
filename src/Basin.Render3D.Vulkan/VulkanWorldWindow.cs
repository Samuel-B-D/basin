using Silk.NET.Vulkan;

namespace Basin.Render3D.Vulkan;

internal sealed class VulkanWorldWindow
{
    public VulkanWorldImage?[] Copies { get; set; } = new VulkanWorldImage?[4];

    public ImageView[] Views { get; set; } = new ImageView[4];

    public Sampler[] Samplers { get; set; } = new Sampler[4];

    public System.Numerics.Vector4[] UvRects { get; set; } = new System.Numerics.Vector4[4];

    public bool[] ForceOpaque { get; set; } = new bool[4];

    public bool[] Ready { get; set; } = new bool[4];

    public long PreparedFrame { get; set; } = -1;

    public ImageView PlaqueView { get; set; }

    public System.Numerics.Vector4 PlaqueUv { get; set; }

    public bool PlaqueOpaque { get; set; }

    public bool PlaqueReady { get; set; }

    public void Grow(int layers)
    {
        if (Views.Length >= layers)
        {
            return;
        }

        var size = Math.Max(layers, Views.Length * 2);
        var copies = Copies;
        var views = Views;
        var samplers = Samplers;
        var rects = UvRects;
        var opaque = ForceOpaque;
        var ready = Ready;
        Array.Resize(ref copies, size);
        Array.Resize(ref views, size);
        Array.Resize(ref samplers, size);
        Array.Resize(ref rects, size);
        Array.Resize(ref opaque, size);
        Array.Resize(ref ready, size);
        Copies = copies;
        Views = views;
        Samplers = samplers;
        UvRects = rects;
        ForceOpaque = opaque;
        Ready = ready;
    }
}
