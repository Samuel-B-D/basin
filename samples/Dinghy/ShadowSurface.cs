using System.Runtime.InteropServices;
using Basin.WindowManager;
using Wayland;

namespace Dinghy;

internal sealed class ShadowSurface : IDisposable
{
    private readonly DecorationSurface _surface;

    private int _width;
    private int _height;
    private int _bufferWidth;
    private int _bufferHeight;
    private (int FrameWidth, int FrameHeight, int ShadowSize, uint Color, int Scale)? _lastKey;

    internal ShadowSurface(WmWindow window, WlCompositor compositor, WlShm shm, OutputScales scales) =>
        _surface = new DecorationSurface(window, compositor, shm, scales, above: false);

    public int ScaleFor(uint fallbackOutputName) => _surface.ScaleFor(fallbackOutputName);

    public void EnsureBuffer(int frameWidth, int frameHeight, int shadowSize, int scale)
    {
        if (frameWidth <= 0 || frameHeight <= 0)
        {
            return;
        }

        shadowSize = Math.Max(shadowSize, 0);
        scale = Math.Max(scale, 1);
        _width = frameWidth + (shadowSize * 2);
        _height = frameHeight + (shadowSize * 2);
        _bufferWidth = _width * scale;
        _bufferHeight = _height * scale;
        _surface.SetBufferScale(scale);
    }

    public void UpdateInputRegion() => _surface.ClearInputRegion();

    public bool Render(int frameWidth, int frameHeight, int shadowSize, uint color, int scale)
    {
        if (_width <= 0 || _height <= 0 || _bufferWidth <= 0 || _bufferHeight <= 0
            || frameWidth <= 0 || frameHeight <= 0)
        {
            return false;
        }

        var key = (frameWidth, frameHeight, Math.Max(shadowSize, 0), color, Math.Max(scale, 1));
        if (_lastKey == key)
        {
            return false;
        }

        if (_surface.Prepare(_bufferWidth, _bufferHeight) == 0)
        {
            return false;
        }

        ShadowNineSlice.Draw(
            _surface.Bytes,
            _bufferWidth,
            frameWidth,
            frameHeight,
            Math.Max(shadowSize, 0),
            Math.Max(shadowSize, 0) / 2,
            color,
            Math.Max(scale, 1));

        _lastKey = key;
        return true;
    }

    public void SetOffset(int x, int y) => _surface.SetOffset(x, y);

    public void Invalidate() => _lastKey = null;

    public void SyncNextCommit() => _surface.SyncNextCommit();

    public void Commit() => _surface.Commit();

    public void Dispose() => _surface.Dispose();
}
