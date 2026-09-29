using Basin.Render.Vulkan;
using Silk.NET.Vulkan;

namespace Basin.Render3D.Vulkan;

internal sealed unsafe class VulkanWorldImage
{
    private VulkanWorldImage(Image image, DeviceMemory memory, ImageView view, ImageView levelZero, int width, int height, uint levels)
    {
        Image = image;
        Memory = memory;
        View = view;
        LevelZero = levelZero;
        Width = width;
        Height = height;
        Levels = levels;
    }

    public Image Image { get; }

    public DeviceMemory Memory { get; }

    public ImageView View { get; }

    public ImageView LevelZero { get; }

    public int Width { get; }

    public int Height { get; }

    public uint Levels { get; }

    public Framebuffer Framebuffer { get; set; }

    public ITexture? Source { get; set; }

    public int Version { get; set; } = -1;

    public ulong LastPoint { get; set; }

    public static uint LevelsFor(int width, int height) => 1 + (uint)Math.Floor(Math.Log2(Math.Max(width, height)));

    public static VulkanWorldImage Create(VulkanDevice device, int width, int height, Format format, ImageUsageFlags usage)
    {
        var vk = device.Api;
        var levels = LevelsFor(width, height);
        var info = new ImageCreateInfo
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Format = format,
            Extent = new Extent3D((uint)width, (uint)height, 1),
            MipLevels = levels,
            ArrayLayers = 1,
            Samples = SampleCountFlags.Count1Bit,
            Tiling = ImageTiling.Optimal,
            Usage = usage | ImageUsageFlags.SampledBit | ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit,
            InitialLayout = ImageLayout.Undefined,
        };
        VulkanDevice.Check(vk.CreateImage(device.Device, in info, null, out var image), "vkCreateImage(world)");
        vk.GetImageMemoryRequirements(device.Device, image, out var requirements);
        var allocate = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = requirements.Size,
            MemoryTypeIndex = device.MemoryTypeFor(requirements.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit),
        };
        VulkanDevice.Check(vk.AllocateMemory(device.Device, in allocate, null, out var memory), "vkAllocateMemory(world image)");
        VulkanDevice.Check(vk.BindImageMemory(device.Device, image, memory, 0), "vkBindImageMemory(world)");
        var view = CreateView(device, image, format, 0, levels);
        var levelZero = CreateView(device, image, format, 0, 1);
        return new VulkanWorldImage(image, memory, view, levelZero, width, height, levels);
    }

    public void RecordMips(Vk vk, CommandBuffer commands, ImageLayout levelZeroLayout)
    {
        Barrier(vk, commands, 0, 1, levelZeroLayout, ImageLayout.TransferSrcOptimal,
            AccessFlags.ColorAttachmentWriteBit | AccessFlags.TransferWriteBit, AccessFlags.TransferReadBit,
            PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.TransferBit, PipelineStageFlags.TransferBit);
        var width = Width;
        var height = Height;
        for (uint level = 1; level < Levels; level++)
        {
            var nextWidth = Math.Max(1, width / 2);
            var nextHeight = Math.Max(1, height / 2);
            Barrier(vk, commands, level, 1, ImageLayout.Undefined, ImageLayout.TransferDstOptimal,
                0, AccessFlags.TransferWriteBit, PipelineStageFlags.TopOfPipeBit, PipelineStageFlags.TransferBit);
            var blit = new ImageBlit
            {
                SrcSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, level - 1, 0, 1),
                DstSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, level, 0, 1),
            };
            blit.SrcOffsets.Element1 = new Offset3D(width, height, 1);
            blit.DstOffsets.Element1 = new Offset3D(nextWidth, nextHeight, 1);
            vk.CmdBlitImage(
                commands, Image, ImageLayout.TransferSrcOptimal, Image, ImageLayout.TransferDstOptimal, 1, &blit, Filter.Linear);
            Barrier(vk, commands, level, 1, ImageLayout.TransferDstOptimal, ImageLayout.TransferSrcOptimal,
                AccessFlags.TransferWriteBit, AccessFlags.TransferReadBit, PipelineStageFlags.TransferBit, PipelineStageFlags.TransferBit);
            width = nextWidth;
            height = nextHeight;
        }

        Barrier(vk, commands, 0, Levels, ImageLayout.TransferSrcOptimal, ImageLayout.General,
            AccessFlags.TransferReadBit, AccessFlags.ShaderReadBit, PipelineStageFlags.TransferBit, PipelineStageFlags.FragmentShaderBit);
    }

    public void Barrier(
        Vk vk, CommandBuffer commands, uint baseLevel, uint count, ImageLayout from, ImageLayout to,
        AccessFlags srcAccess, AccessFlags dstAccess, PipelineStageFlags srcStage, PipelineStageFlags dstStage)
    {
        var barrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            SrcAccessMask = srcAccess,
            DstAccessMask = dstAccess,
            OldLayout = from,
            NewLayout = to,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = Image,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, baseLevel, count, 0, 1),
        };
        vk.CmdPipelineBarrier(commands, srcStage, dstStage, 0, 0, null, 0, null, 1, &barrier);
    }

    public void Destroy(VulkanDevice device)
    {
        var vk = device.Api;
        if (Framebuffer.Handle != 0)
        {
            vk.DestroyFramebuffer(device.Device, Framebuffer, null);
        }

        vk.DestroyImageView(device.Device, LevelZero, null);
        vk.DestroyImageView(device.Device, View, null);
        vk.DestroyImage(device.Device, Image, null);
        vk.FreeMemory(device.Device, Memory, null);
    }

    private static ImageView CreateView(VulkanDevice device, Image image, Format format, uint baseLevel, uint levels)
    {
        var info = new ImageViewCreateInfo
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = image,
            ViewType = ImageViewType.Type2D,
            Format = format,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, baseLevel, levels, 0, 1),
        };
        VulkanDevice.Check(device.Api.CreateImageView(device.Device, in info, null, out var view), "vkCreateImageView(world)");
        return view;
    }
}
