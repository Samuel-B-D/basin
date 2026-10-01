using System.Drawing;
using Prowl.Quill;
using Prowl.Vector;

namespace Basin.Frames.MsStyles.Quill;

internal sealed class MsStylesQuillTextures
{
    private readonly ICanvasRenderer _renderer;
    private readonly Dictionary<(int Handle, Rectangle Source, int ScaleX, int ScaleY), object> _pieces = [];
    private readonly Dictionary<MsStylesGradient, object> _gradients = [];
    private readonly Dictionary<object, object> _icons = new(ReferenceEqualityComparer.Instance);

    public MsStylesQuillTextures(ICanvasRenderer renderer)
    {
        _renderer = renderer;
    }

    public object Piece(MsStylesBitmap bitmap, Rectangle source, int scaleX, int scaleY)
    {
        var key = (bitmap.Handle, source, scaleX, scaleY);
        if (_pieces.TryGetValue(key, out var texture))
        {
            return texture;
        }

        var width = source.Width * scaleX;
        var height = source.Height * scaleY;
        var rgba = new byte[width * height * 4];
        var pixels = bitmap.Pixels;
        for (var y = 0; y < height; y++)
        {
            var sy = source.Y + (y / scaleY);
            for (var x = 0; x < width; x++)
            {
                var sx = source.X + (x / scaleX);
                var from = ((sy * bitmap.Width) + sx) * 4;
                var to = ((y * width) + x) * 4;
                rgba[to] = pixels[from + 2];
                rgba[to + 1] = pixels[from + 1];
                rgba[to + 2] = pixels[from];
                rgba[to + 3] = pixels[from + 3];
            }
        }

        texture = Upload(width, height, rgba);
        _pieces[key] = texture;
        return texture;
    }

    public object Gradient(in MsStylesGradient gradient)
    {
        if (_gradients.TryGetValue(gradient, out var texture))
        {
            return texture;
        }

        var (width, height, rgba) = MsStylesQuillGradients.Rasterize(gradient);
        texture = Upload(width, height, rgba);
        _gradients[gradient] = texture;
        return texture;
    }

    public object Icon(object key, int width, int height, byte[] rgba)
    {
        if (_icons.TryGetValue(key, out var texture))
        {
            return texture;
        }

        texture = Upload(width, height, rgba);
        _icons[key] = texture;
        return texture;
    }

    public void Forget(object key) => _icons.Remove(key);

    public MsStylesQuillGlow Glow { get; } = new();

    public ICanvasRenderer Renderer => _renderer;

    private object Upload(int width, int height, byte[] rgba)
    {
        var texture = _renderer.CreateTexture((uint)width, (uint)height);
        _renderer.SetTextureData(texture, new IntRect(0, 0, width, height), rgba);
        return texture;
    }
}
