using Basin.Render.Vulkan;
using Basin.Scene3D;
using Silk.NET.Vulkan;

namespace Basin.Render3D.Vulkan;

internal sealed unsafe class VulkanWorldMesh
{
    private VulkanWorldMesh(VulkanWorldBuffer vertices, VulkanWorldBuffer indices, int indexCount)
    {
        Vertices = vertices;
        Indices = indices;
        IndexCount = indexCount;
    }

    public VulkanWorldBuffer Vertices { get; }

    public VulkanWorldBuffer Indices { get; }

    public int IndexCount { get; }

    public long LastUsed { get; set; }

    public ulong LastPoint { get; set; }

    public static VulkanWorldMesh Create(VulkanDevice device, WorldMesh mesh) =>
        Create<WorldVertex>(device, mesh.Vertices, mesh.Indices);

    public static VulkanWorldMesh Create(VulkanDevice device, SkinnedMesh mesh) =>
        Create<SkinnedVertex>(device, mesh.Vertices, mesh.Indices);

    public void Draw(Vk vk, CommandBuffer commands)
    {
        var buffer = Vertices.Handle;
        ulong offset = 0;
        vk.CmdBindVertexBuffers(commands, 0, 1, &buffer, &offset);
        vk.CmdBindIndexBuffer(commands, Indices.Handle, 0, IndexType.Uint32);
        vk.CmdDrawIndexed(commands, (uint)IndexCount, 1, 0, 0, 0);
    }

    public void Destroy(VulkanDevice device)
    {
        Vertices.Destroy(device);
        Indices.Destroy(device);
    }

    private static VulkanWorldMesh Create<TVertex>(VulkanDevice device, TVertex[] vertices, uint[] indices)
        where TVertex : unmanaged
    {
        var vertexBytes = (ulong)(vertices.Length * sizeof(TVertex));
        var indexBytes = (ulong)(indices.Length * sizeof(uint));
        var vertexBuffer = VulkanWorldBuffer.Create(device, Math.Max(vertexBytes, 16), BufferUsageFlags.VertexBufferBit);
        var indexBuffer = VulkanWorldBuffer.Create(device, Math.Max(indexBytes, 16), BufferUsageFlags.IndexBufferBit);
        fixed (TVertex* source = vertices)
        {
            System.Buffer.MemoryCopy(source, vertexBuffer.Mapped, (long)vertexBuffer.Size, (long)vertexBytes);
        }

        fixed (uint* source = indices)
        {
            System.Buffer.MemoryCopy(source, indexBuffer.Mapped, (long)indexBuffer.Size, (long)indexBytes);
        }

        return new VulkanWorldMesh(vertexBuffer, indexBuffer, indices.Length);
    }
}
