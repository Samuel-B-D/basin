using Basin;
using Basin.Scene;
using SkiaSharp;
using System7.Avalonia.Rendering;

namespace System7Wm;

internal sealed class DesktopPattern : IDisposable
{
    private readonly SceneTree _parent;
    private readonly Dictionary<IOutput, (SceneBuffer Node, MemoryBuffer Buffer)> _outputs = [];
    private SKBitmap? _wallpaper;
    private string _wallpaperPath = "";

    public DesktopPattern(SceneTree parent)
    {
        _parent = parent;
    }

    public System7DesktopPattern Pattern { get; set; } = System7DesktopPattern.Default;

    public System7ColorDepth Depth { get; set; } = System7ColorDepth.Indexed8;

    public string Wallpaper
    {
        get => _wallpaperPath;
        set
        {
            if (value == _wallpaperPath)
            {
                return;
            }

            _wallpaperPath = value;
            _wallpaper?.Dispose();
            _wallpaper = value.Length > 0 && File.Exists(value) ? SKBitmap.Decode(value) : null;
        }
    }

    public void Place(IOutput output, in Box logical)
    {
        var mode = output.CurrentMode;
        if (mode.Width <= 0 || mode.Height <= 0)
        {
            return;
        }

        if (_outputs.TryGetValue(output, out var existing) && existing.Buffer.Width == mode.Width && existing.Buffer.Height == mode.Height)
        {
            existing.Node.SetPosition(logical.X, logical.Y);
            existing.Node.DestinationWidth = logical.Width;
            existing.Node.DestinationHeight = logical.Height;
            existing.Node.DeviceScale = output.Scale;
            Paint(existing.Buffer, output.Scale);
            existing.Node.SetBuffer(existing.Buffer);
            existing.Node.NotifyContentChanged();
            return;
        }

        Remove(output);
        var buffer = new MemoryBuffer(mode.Width, mode.Height, DrmFormat.Xrgb8888);
        var node = new SceneBuffer(_parent) { IsOpaque = true, InputEnabled = false };
        node.SetBuffer(buffer);
        node.SetPosition(logical.X, logical.Y);
        node.DestinationWidth = logical.Width;
        node.DestinationHeight = logical.Height;
        node.DevicePixels = (mode.Width, mode.Height);
        node.DeviceScale = output.Scale;
        Paint(buffer, output.Scale);
        node.NotifyContentChanged();
        _outputs[output] = (node, buffer);
    }

    public void Repaint(Func<IOutput, Box> boxOf)
    {
        foreach (var output in _outputs.Keys.ToList())
        {
            Place(output, boxOf(output));
        }
    }

    public void Remove(IOutput output)
    {
        if (_outputs.Remove(output, out var entry))
        {
            entry.Node.SetBuffer(null);
            entry.Node.Destroy();
            entry.Buffer.Destroy();
        }
    }

    public void Dispose()
    {
        foreach (var output in _outputs.Keys.ToList())
        {
            Remove(output);
        }

        _wallpaper?.Dispose();
    }

    private unsafe void Paint(MemoryBuffer buffer, double scale)
    {
        if (!buffer.BeginDataAccess(BufferDataAccess.Write, out var view))
        {
            return;
        }

        try
        {
            if (_wallpaper is { } image)
            {
                using var bitmap = new SKBitmap(new SKImageInfo(buffer.Width, buffer.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
                using (var canvas = new SKCanvas(bitmap))
                {
                    var cover = Math.Max((float)buffer.Width / image.Width, (float)buffer.Height / image.Height);
                    var width = image.Width * cover;
                    var height = image.Height * cover;
                    using var picture = SKImage.FromBitmap(image);
                    canvas.DrawImage(picture, new SKRect((buffer.Width - width) / 2, (buffer.Height - height) / 2, (buffer.Width + width) / 2, (buffer.Height + height) / 2), new SKSamplingOptions(SKFilterMode.Linear));
                }

                var source = (uint*)bitmap.GetPixels();
                for (var y = 0; y < buffer.Height; y++)
                {
                    var row = (uint*)((byte*)view.Data + (y * view.Stride));
                    for (var x = 0; x < buffer.Width; x++)
                    {
                        row[x] = DepthColors.Map(source[(y * buffer.Width) + x] | 0xff000000u, Depth);
                    }
                }

                return;
            }

            var chrome = DevicePlacement.Chrome(scale);
            var pattern = Pattern;
            var monochrome = Depth == System7ColorDepth.Monochrome || !pattern.HasColor;
            var tileWidth = monochrome ? 8 : pattern.Width;
            var tileHeight = monochrome ? 8 : pattern.Height;
            var tile = new uint[tileWidth * tileHeight];
            for (var y = 0; y < tileHeight; y++)
            {
                for (var x = 0; x < tileWidth; x++)
                {
                    var argb = monochrome ? (pattern.IsSet(x, y) ? 0xff000000u : 0xffffffffu) : pattern.Argb(x, y);
                    tile[(y * tileWidth) + x] = monochrome ? argb : DepthColors.Map(argb, Depth);
                }
            }

            for (var y = 0; y < buffer.Height; y++)
            {
                var row = (uint*)((byte*)view.Data + (y * view.Stride));
                var ty = (y / chrome) % tileHeight;
                for (var x = 0; x < buffer.Width; x++)
                {
                    row[x] = tile[(ty * tileWidth) + ((x / chrome) % tileWidth)];
                }
            }
        }
        finally
        {
            buffer.EndDataAccess();
        }
    }
}
