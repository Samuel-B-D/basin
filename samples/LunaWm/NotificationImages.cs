using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Basin.Notifications;

namespace LunaWm;

internal sealed class NotificationImages : IDisposable
{
    private readonly Dictionary<uint, WriteableBitmap?> _images = [];

    public WriteableBitmap? For(Notification notification)
    {
        if (notification.Image is not { } image)
        {
            return null;
        }

        if (!_images.TryGetValue(notification.Id, out var bitmap))
        {
            bitmap = Convert(image);
            _images[notification.Id] = bitmap;
        }

        return bitmap;
    }

    public void Forget(uint id)
    {
        if (_images.Remove(id, out var bitmap))
        {
            bitmap?.Dispose();
        }
    }

    public static unsafe WriteableBitmap? Convert(NotificationImage image)
    {
        var channels = image.HasAlpha ? 4 : 3;
        if (image.Width <= 0 || image.Height <= 0 || image.BitsPerSample != 8 || image.Channels != channels ||
            image.RowStride < image.Width * channels || image.Pixels.Length < ((image.Height - 1) * image.RowStride) + (image.Width * channels))
        {
            return null;
        }

        var bitmap = new WriteableBitmap(new PixelSize(image.Width, image.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var frame = bitmap.Lock();
        var source = image.Pixels.Span;
        for (var y = 0; y < image.Height; y++)
        {
            var row = (byte*)frame.Address + (y * frame.RowBytes);
            var from = y * image.RowStride;
            for (var x = 0; x < image.Width; x++)
            {
                var r = source[from];
                var g = source[from + 1];
                var b = source[from + 2];
                var a = channels == 4 ? source[from + 3] : (byte)255;
                from += channels;
                row[(x * 4) + 0] = (byte)(b * a / 255);
                row[(x * 4) + 1] = (byte)(g * a / 255);
                row[(x * 4) + 2] = (byte)(r * a / 255);
                row[(x * 4) + 3] = a;
            }
        }

        return bitmap;
    }

    public void Dispose()
    {
        foreach (var bitmap in _images.Values)
        {
            bitmap?.Dispose();
        }

        _images.Clear();
    }
}
