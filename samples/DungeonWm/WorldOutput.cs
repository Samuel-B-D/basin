using Basin;
using Basin.Host;
using Basin.Scene;
using Basin.Scene3D;

namespace DungeonWm;

internal sealed class WorldOutput : IDisposable
{
    private const int MaxBuffers = 4;

    private readonly List<IBuffer> _ring = [];
    private readonly IAllocator _allocator;
    private int _width;
    private int _height;

    public WorldOutput(OutputView view, SceneTree layer, IAllocator allocator)
    {
        View = view;
        _allocator = allocator;
        Node = new SceneBuffer(layer) { IsOpaque = true, InputEnabled = false, ExcludedFromCapture = false };
        Node.LowerToBottom();
    }

    public OutputView View { get; }

    public SceneBuffer Node { get; }

    public WorldVisibleSet Visible { get; } = new();

    public long Frames { get; private set; }

    public bool Render(IWorldRenderer renderer, World world, WorldCamera camera, in Box union, in Box slice, FrameTick tick, WorldRenderSettings settings)
    {
        var width = Math.Max(1, View.Width);
        var height = Math.Max(1, View.Height);
        if (width != _width || height != _height)
        {
            DropRing();
            (_width, _height) = (width, height);
        }

        if (Take() is not { } target)
        {
            return false;
        }

        var worldView = camera.ViewOf(union, slice);
        Visible.Clear();
        WorldVisibility.Collect(world, worldView, settings.RoomDepth, Visible);
        if (!renderer.Render(new WorldFrame(world, worldView, tick, Visible, settings), target))
        {
            return false;
        }

        Node.SetPosition(slice.X, slice.Y);
        Node.SetBuffer(target);
        Node.DestinationWidth = slice.Width;
        Node.DestinationHeight = slice.Height;
        Node.NotifyContentChanged();
        Frames++;
        return true;
    }

    public void Dispose()
    {
        Node.Destroy();
        foreach (var buffer in _ring)
        {
            if (buffer is BufferBase concrete && !concrete.IsDestroyed)
            {
                concrete.Destroy();
            }
        }

        _ring.Clear();
    }

    private IBuffer? Take()
    {
        foreach (var buffer in _ring)
        {
            if (buffer.LockCount == 0 && !ReferenceEquals(buffer, Node.Buffer))
            {
                return buffer;
            }
        }

        if (_ring.Count >= MaxBuffers)
        {
            return null;
        }

        var modifiers = View.SwapModifiers.Length > 0 ? View.SwapModifiers : [DrmFormatSet.ModifierLinear];
        var allocated = _allocator.Allocate(_width, _height, DrmFormat.Xrgb8888, modifiers, BufferUse.Render | BufferUse.Scanout)
            ?? _allocator.Allocate(_width, _height, DrmFormat.Xrgb8888, [DrmFormatSet.ModifierLinear], BufferUse.Render);
        if (allocated is null)
        {
            return null;
        }

        _ring.Add(allocated);
        return allocated;
    }

    private void DropRing()
    {
        foreach (var buffer in _ring)
        {
            if (!ReferenceEquals(buffer, Node.Buffer) && buffer is BufferBase concrete && !concrete.IsDestroyed)
            {
                concrete.Destroy();
            }
        }

        _ring.RemoveAll(buffer => !ReferenceEquals(buffer, Node.Buffer));
    }
}
