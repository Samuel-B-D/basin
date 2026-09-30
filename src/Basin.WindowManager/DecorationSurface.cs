using Wayland;

namespace Basin.WindowManager;

public sealed class DecorationSurface : IDisposable
{
    private readonly WlCompositor _compositor;
    private readonly WlSurface _surface;
    private readonly WmDecoration _decoration;
    private readonly ShmSlots _slots;
    private readonly OutputScales _scales;
    private readonly HashSet<WlOutput> _entered = [];
    private WmBackgroundEffects? _effects;
    private Box _blurred;
    private bool _disposed;

    public DecorationSurface(WmWindow window, WlCompositor compositor, WlShm shm, OutputScales scales, bool above)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(compositor);
        ArgumentNullException.ThrowIfNull(shm);
        ArgumentNullException.ThrowIfNull(scales);
        _compositor = compositor;
        _scales = scales;
        _surface = compositor.CreateSurface();
        _surface.Enter += (_, e) =>
        {
            if (e.Output is not null)
            {
                _entered.Add(e.Output);
            }
        };
        _surface.Leave += (_, e) =>
        {
            if (e.Output is not null)
            {
                _entered.Remove(e.Output);
            }
        };
        _decoration = above ? window.CreateDecorationAbove(_surface) : window.CreateDecorationBelow(_surface);
        _slots = new ShmSlots(shm);
    }

    public WlSurface Surface => _surface;

    public uint SurfaceId => _surface.Id;

    public bool Mapped { get; private set; }

    public bool HasBuffer => _slots.CurrentBuffer is not null;

    public int BufferWidth { get; private set; }

    public int BufferHeight { get; private set; }

    public int ScaleFor(uint fallbackOutputName)
    {
        if (_entered.Count == 0)
        {
            return _scales.ScaleForName(fallbackOutputName);
        }

        var scale = 1;
        foreach (var output in _entered)
        {
            scale = Math.Max(scale, _scales.ScaleFor(output));
        }

        return scale;
    }

    public void SetBufferScale(int scale) => _surface.SetBufferScale(Math.Max(scale, 1));

    public void SetOffset(int x, int y) => _decoration.SetOffset(x, y);

    public void SyncNextCommit() => _decoration.SyncNextCommit();

    public void ClearInputRegion() => SetInputRegion([], []);

    public void SetInputRegion(ReadOnlySpan<Rect> add, ReadOnlySpan<Rect> subtract)
    {
        var region = _compositor.CreateRegion();
        foreach (var rect in add)
        {
            region.Add(rect.X, rect.Y, rect.Width, rect.Height);
        }

        foreach (var rect in subtract)
        {
            region.Subtract(rect.X, rect.Y, rect.Width, rect.Height);
        }

        _surface.SetInputRegion(region);
        region.Destroy();
    }

    public bool SetBlur(WmBackgroundEffects effects, Box region)
    {
        ArgumentNullException.ThrowIfNull(effects);
        _effects = effects;
        if (region == _blurred)
        {
            return false;
        }

        _blurred = region;
        effects.SetBlurRegion(_surface, region.IsEmpty ? [] : [region]);
        return true;
    }

    public nint Prepare(int bufferWidth, int bufferHeight)
    {
        if (bufferWidth <= 0 || bufferHeight <= 0)
        {
            return 0;
        }

        var pixels = _slots.Prepare(bufferWidth, bufferHeight, bufferWidth * 4);
        if (pixels == 0)
        {
            return 0;
        }

        BufferWidth = bufferWidth;
        BufferHeight = bufferHeight;
        _slots.CurrentBytes().Clear();
        return pixels;
    }

    public Span<byte> Bytes => _slots.CurrentBytes();

    public void Commit() => Commit([new Rect(0, 0, BufferWidth, BufferHeight)]);

    public void Commit(ReadOnlySpan<Rect> damage)
    {
        if (_slots.CurrentBuffer is not { } buffer)
        {
            return;
        }

        _surface.Attach(buffer, 0, 0);
        foreach (var rect in damage)
        {
            _surface.DamageBuffer(rect.X, rect.Y, rect.Width, rect.Height);
        }

        _surface.Commit();
        _slots.MarkAttached();
        Mapped = true;
    }

    public void Unmap()
    {
        _surface.Attach(null, 0, 0);
        _surface.Commit();
        Mapped = false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _effects?.Forget(_surface);
        _slots.Dispose();
        _decoration.Dispose();
        if (!_surface.IsDestroyed)
        {
            _surface.Destroy();
        }
    }
}
