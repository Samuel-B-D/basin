using Basin.WindowManager.Protocol;

namespace Basin.WindowManager;

public readonly record struct DimensionsHint(Size Minimum, Size Maximum)
{
    public bool IsFixed =>
        Minimum.Width > 0 && Minimum.Width == Maximum.Width
        && Minimum.Height > 0 && Minimum.Height == Maximum.Height;

    public Size Clamp(Size size)
    {
        var width = size.Width;
        var height = size.Height;
        if (Maximum.Width > 0)
        {
            width = Math.Min(width, Maximum.Width);
        }

        if (Maximum.Height > 0)
        {
            height = Math.Min(height, Maximum.Height);
        }

        return new Size(Math.Max(width, Minimum.Width), Math.Max(height, Minimum.Height));
    }
}
