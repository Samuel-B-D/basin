using Basin.Render.Vulkan;
using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Basin.Render3D.Vulkan;

internal sealed unsafe class VulkanWorldFrame : IDisposable
{
    private const uint FirstPoolSets = 64;
    private const ulong FirstUniformBytes = 256 * 1024;

    private readonly VulkanDevice _device;
    private readonly VulkanWorldPipelines _pipelines;
    private readonly List<DescriptorPool> _pools = new(8);
    private readonly List<VulkanWorldBuffer> _stale = new(4);
    private readonly Dictionary<VulkanWorldSetKey, DescriptorSet> _sets = new(256);
    private int _pool;
    private uint _lastPoolSets;
    private readonly List<Semaphore> _waits = new(8);
    private VulkanWorldBuffer? _uniforms;
    private ulong _used;
    private int _waitCount;

    public VulkanWorldFrame(VulkanDevice device, VulkanWorldPipelines pipelines)
    {
        _device = device;
        _pipelines = pipelines;
    }

    public ulong Point { get; set; }

    public bool IsIdle => Point == 0 || _device.IsComplete(Point);

    public void Reset()
    {
        foreach (var buffer in _stale)
        {
            buffer.Destroy(_device);
        }

        _stale.Clear();
        foreach (var pool in _pools)
        {
            _ = _device.Api.ResetDescriptorPool(_device.Device, pool, 0);
        }

        _sets.Clear();
        _pool = 0;
        _used = 0;
        _waitCount = 0;
    }

    public ReadOnlySpan<Semaphore> Waits => System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_waits)[.._waitCount];

    public void AddWait(Silk.NET.Vulkan.Extensions.KHR.KhrExternalSemaphoreFd? semaphores, int syncFile)
    {
        if (syncFile < 0)
        {
            return;
        }

        if (semaphores is { } ext)
        {
            if (_waitCount == _waits.Count)
            {
                var info = new SemaphoreCreateInfo { SType = StructureType.SemaphoreCreateInfo };
                VulkanDevice.Check(_device.Api.CreateSemaphore(_device.Device, in info, null, out var created), "vkCreateSemaphore(world wait)");
                _waits.Add(created);
            }

            var import = new ImportSemaphoreFdInfoKHR
            {
                SType = StructureType.ImportSemaphoreFDInfoKhr,
                Semaphore = _waits[_waitCount],
                Flags = SemaphoreImportFlags.TemporaryBit,
                HandleType = ExternalSemaphoreHandleTypeFlags.SyncFDBit,
                Fd = syncFile,
            };
            if (ext.ImportSemaphoreF(_device.Device, in import) == Result.Success)
            {
                _waitCount++;
                return;
            }
        }

        _ = RenderFences.WaitSyncFile(syncFile);
        RenderFences.CloseFence(syncFile);
    }

    public VulkanWorldBuffer Reserve(ulong bytes, ulong alignment, out ulong offset)
    {
        var start = (_used + alignment - 1) / alignment * alignment;
        if (_uniforms is null || _uniforms.Size - start < bytes + VulkanWorldPipelines.JointsSize)
        {
            var capacity = Math.Max(FirstUniformBytes, _uniforms is null ? 0 : _uniforms.Size * 2);
            if (_uniforms is not null)
            {
                _stale.Add(_uniforms);
            }

            _uniforms = VulkanWorldBuffer.Create(_device, capacity, BufferUsageFlags.UniformBufferBit);
            start = 0;
        }

        _used = start + bytes;
        offset = start;
        return _uniforms;
    }

    public VulkanWorldBuffer Current => _uniforms ?? Reserve(0, 16, out _);

    public DescriptorSet SetFor(in VulkanWorldSetKey key, ImageView view, Sampler sampler,
        VulkanWorldBuffer draw, VulkanWorldBuffer scene, VulkanWorldBuffer joints)
    {
        if (_sets.TryGetValue(key, out var set))
        {
            return set;
        }

        set = Allocate(_pipelines.SetLayout);
        var buffers = stackalloc DescriptorBufferInfo[3];
        buffers[0] = new DescriptorBufferInfo(draw.Handle, 0, VulkanWorldPipelines.DrawSize);
        buffers[1] = new DescriptorBufferInfo(scene.Handle, 0, VulkanWorldPipelines.SceneSize);
        buffers[2] = new DescriptorBufferInfo(joints.Handle, 0, VulkanWorldPipelines.JointsSize);
        var image = new DescriptorImageInfo(sampler, view, ImageLayout.General);
        var writes = stackalloc WriteDescriptorSet[4];
        writes[0] = Write(set, 0, DescriptorType.UniformBufferDynamic, &buffers[0], null);
        writes[1] = Write(set, 1, DescriptorType.UniformBufferDynamic, &buffers[1], null);
        writes[2] = Write(set, 2, DescriptorType.CombinedImageSampler, null, &image);
        writes[3] = Write(set, 3, DescriptorType.UniformBufferDynamic, &buffers[2], null);
        _device.Api.UpdateDescriptorSets(_device.Device, 4, writes, 0, null);
        _sets[key] = set;
        return set;
    }

    public DescriptorSet CopySet(ImageView view, Sampler sampler)
    {
        var set = Allocate(_pipelines.CopySetLayout);
        var image = new DescriptorImageInfo(sampler, view, ImageLayout.General);
        var write = Write(set, 0, DescriptorType.CombinedImageSampler, null, &image);
        _device.Api.UpdateDescriptorSets(_device.Device, 1, &write, 0, null);
        return set;
    }

    public void Dispose()
    {
        Reset();
        foreach (var pool in _pools)
        {
            _device.Api.DestroyDescriptorPool(_device.Device, pool, null);
        }

        _pools.Clear();
        foreach (var semaphore in _waits)
        {
            _device.Api.DestroySemaphore(_device.Device, semaphore, null);
        }

        _waits.Clear();
        _uniforms?.Destroy(_device);
        _uniforms = null;
    }

    private static WriteDescriptorSet Write(
        DescriptorSet set, uint binding, DescriptorType type, DescriptorBufferInfo* buffer, DescriptorImageInfo* image) => new()
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = set,
            DstBinding = binding,
            DescriptorCount = 1,
            DescriptorType = type,
            PBufferInfo = buffer,
            PImageInfo = image,
        };

    private DescriptorSet Allocate(DescriptorSetLayout layout)
    {
        while (true)
        {
            if (_pool == _pools.Count)
            {
                _pools.Add(CreatePool());
            }

            var info = new DescriptorSetAllocateInfo
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = _pools[_pool],
                DescriptorSetCount = 1,
                PSetLayouts = &layout,
            };
            var result = _device.Api.AllocateDescriptorSets(_device.Device, in info, out var set);
            if (result == Result.Success)
            {
                return set;
            }

            if (result is not (Result.ErrorOutOfPoolMemory or Result.ErrorFragmentedPool))
            {
                VulkanDevice.Check(result, "vkAllocateDescriptorSets(world)");
            }

            _pool++;
        }
    }

    private DescriptorPool CreatePool()
    {
        var sets = _lastPoolSets == 0 ? FirstPoolSets : _lastPoolSets * 2;
        _lastPoolSets = sets;
        var sizes = stackalloc DescriptorPoolSize[2];
        sizes[0] = new DescriptorPoolSize(DescriptorType.UniformBufferDynamic, sets * 3);
        sizes[1] = new DescriptorPoolSize(DescriptorType.CombinedImageSampler, sets);
        var info = new DescriptorPoolCreateInfo
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            MaxSets = sets,
            PoolSizeCount = 2,
            PPoolSizes = sizes,
        };
        VulkanDevice.Check(_device.Api.CreateDescriptorPool(_device.Device, in info, null, out var pool), "vkCreateDescriptorPool(world)");
        return pool;
    }
}
