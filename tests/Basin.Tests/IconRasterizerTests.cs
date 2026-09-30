using Basin.Icons.Skia;
using Basin.Render.Skia;
using SkiaSharp;
using Xunit;

namespace Basin.Tests;

public sealed class IconRasterizerTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("basin-icons-").FullName;

    [Fact]
    public void An_svg_is_fitted_and_centered_in_straight_argb()
    {
        var path = Path.Combine(_root, "wide.svg");
        File.WriteAllText(path, "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"40\" height=\"20\"><rect width=\"40\" height=\"20\" fill=\"#ff0000\" fill-opacity=\"0.5\"/></svg>");

        var pixels = IconRasterizer.Argb(path, 16);

        Assert.NotNull(pixels);
        Assert.Equal(0u, pixels[(2 * 16) + 8]);
        var center = pixels[(8 * 16) + 8];
        Assert.InRange(center >> 24, 126u, 129u);
        Assert.Equal(0xffu, (center >> 16) & 0xff);
        Assert.Equal(0u, center & 0xffff);
    }

    [Fact]
    public void A_bitmap_keeps_its_aspect_and_an_image_counts_through_the_census()
    {
        var path = Path.Combine(_root, "tall.png");
        using (var bitmap = new SKBitmap(new SKImageInfo(10, 20, SKColorType.Bgra8888, SKAlphaType.Premul)))
        {
            bitmap.Erase(SKColors.Blue);
            using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(path, data.ToArray());
        }

        var image = IconRasterizer.Image(path, 20);
        Assert.NotNull(image);
        using var pixels = new SKBitmap(20, 20, SKColorType.Bgra8888, SKAlphaType.Premul);
        Assert.True(image.ReadPixels(pixels.Info, pixels.GetPixels(), pixels.RowBytes, 0, 0));
        Assert.Equal(SKColor.Empty, pixels.GetPixel(1, 10));
        Assert.Equal(SKColors.Blue, pixels.GetPixel(10, 10));
        SkiaCensus.Release(image);
    }

    [Fact]
    public void A_missing_or_empty_path_draws_nothing()
    {
        Assert.Null(IconRasterizer.Argb(Path.Combine(_root, "absent.svg"), 16));
        Assert.Null(IconRasterizer.Image(string.Empty, 16));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
