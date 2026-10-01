using System.Drawing;
using Basin.Frames.MsStyles;
using Basin.Themes.MsStyles;
using SkiaSharp;
using Xunit;

namespace Basin.Tests;

public sealed class MsStylesDrawingTests
{
    private const uint Red = 0xffff0000;
    private const uint Green = 0xff00ff00;
    private const uint Blue = 0xff0000ff;
    private const uint Yellow = 0xffffff00;
    private const uint Cyan = 0xff00ffff;
    private const uint White = 0xffffffff;
    private const uint Black = 0xff000000;
    private const uint Gray = 0xff808080;
    private const uint Orange = 0xffff8000;
    private const uint Purple = 0xff8000ff;
    private const uint Magenta = 0xffff00ff;

    private static readonly uint[] NineSlice =
    [
        Red, Red, Cyan, Cyan, Green, Green,
        Red, Red, Cyan, Cyan, Green, Green,
        Orange, Orange, Gray, Black, Purple, Purple,
        Orange, Orange, Black, Gray, Purple, Purple,
        Blue, Blue, White, White, Yellow, Yellow,
        Blue, Blue, White, White, Yellow, Yellow,
    ];

    private sealed class Rig : IDisposable
    {
        public Rig(string section, IReadOnlyDictionary<string, byte[]>? bitmaps = null, int width = 10, int height = 10)
        {
            Theme = MsStylesTheme.Parse(MsStylesWriter.V3("[globals]\n" + section, bitmaps));
            Class = Theme.OpenClass("Button")!;
            Images = new MsStylesImageCache();
            Resources = new MsStylesResources(Images);
            Surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul))!;
        }

        public MsStylesTheme Theme { get; }

        public MsStylesClass Class { get; }

        public MsStylesImageCache Images { get; }

        public MsStylesResources Resources { get; }

        public SKSurface Surface { get; }

        public MsStylesDrawList List { get; } = new();

        public MsStylesDrawContext Context(double dpi = 96) => new(Images, dpi);

        public SKBitmap Draw(int state, RectangleF destination, double dpi = 96, int frame = 0)
        {
            List.Clear();
            MsStylesDrawing.Background(List, Class, 1, state, destination, Context(dpi), frame);
            Surface.Canvas.Clear(SKColors.Transparent);
            MsStylesSkiaPainter.Paint(Surface.Canvas, List, Resources);
            Surface.Canvas.Flush();
            var bitmap = new SKBitmap(Surface.Canvas.DeviceClipBounds.Width, Surface.Canvas.DeviceClipBounds.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            Surface.ReadPixels(bitmap.Info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0);
            return bitmap;
        }

        public void Dispose()
        {
            Surface.Dispose();
            Resources.Dispose();
        }
    }

    private static Dictionary<string, byte[]> Image(string name, int width, int height, uint[] pixels, bool alpha = false) =>
        new() { [name] = MsStylesWriter.Dib(width, height, pixels, alpha) };

    private const string Stretch = """
        [Button.PushButton]
        BgType = ImageFile
        ImageFile = test\slice.bmp
        SizingMargins = 2, 2, 2, 2
        SizingType = Stretch
        """;

    [Fact]
    public void Stretch_keeps_corners_one_to_one_and_stretches_the_rest()
    {
        using var rig = new Rig(Stretch, Image(@"test\slice.bmp", 6, 6, NineSlice));
        using var pixels = rig.Draw(1, new RectangleF(0, 0, 10, 10));

        Assert.Equal(Red, Pixel(pixels, 0, 0));
        Assert.Equal(Red, Pixel(pixels, 1, 1));
        Assert.Equal(Green, Pixel(pixels, 9, 0));
        Assert.Equal(Blue, Pixel(pixels, 0, 9));
        Assert.Equal(Yellow, Pixel(pixels, 8, 8));
        Assert.Equal(Cyan, Pixel(pixels, 2, 0));
        Assert.Equal(Cyan, Pixel(pixels, 7, 1));
        Assert.Equal(Orange, Pixel(pixels, 0, 5));
        Assert.Equal(Purple, Pixel(pixels, 9, 5));
        Assert.Equal(White, Pixel(pixels, 5, 9));
        Assert.Equal(Gray, Pixel(pixels, 2, 2));
        Assert.Equal(Black, Pixel(pixels, 7, 2));
        Assert.Equal(Gray, Pixel(pixels, 7, 7));
    }

    [Fact]
    public void Tile_repeats_the_center_at_its_own_size()
    {
        using var rig = new Rig(Stretch.Replace("SizingType = Stretch", "SizingType = Tile", StringComparison.Ordinal), Image(@"test\slice.bmp", 6, 6, NineSlice));
        using var pixels = rig.Draw(1, new RectangleF(0, 0, 10, 10));

        Assert.Equal(Gray, Pixel(pixels, 2, 2));
        Assert.Equal(Black, Pixel(pixels, 3, 2));
        Assert.Equal(Gray, Pixel(pixels, 4, 2));
        Assert.Equal(Black, Pixel(pixels, 4, 3));
        Assert.Equal(Gray, Pixel(pixels, 7, 7));
        Assert.Equal(Red, Pixel(pixels, 0, 0));
    }

    [Fact]
    public void Margins_shrink_in_proportion_under_a_small_destination()
    {
        using var rig = new Rig(Stretch, Image(@"test\slice.bmp", 6, 6, NineSlice));
        using var pixels = rig.Draw(1, new RectangleF(0, 0, 3, 3));

        Assert.Equal(Red, Pixel(pixels, 0, 0));
        Assert.Equal(Blue, Pixel(pixels, 0, 1));
        Assert.Equal(Blue, Pixel(pixels, 0, 2));
        Assert.Equal(Green, Pixel(pixels, 2, 0));
        Assert.Equal(Yellow, Pixel(pixels, 2, 2));
    }

    [Fact]
    public void BorderOnly_leaves_the_center_empty()
    {
        using var rig = new Rig(Stretch + "\nBorderOnly = True", Image(@"test\slice.bmp", 6, 6, NineSlice));
        using var pixels = rig.Draw(1, new RectangleF(0, 0, 10, 10));

        Assert.Equal(0u, Pixel(pixels, 5, 5));
        Assert.Equal(Cyan, Pixel(pixels, 5, 0));
    }

    [Fact]
    public void The_magenta_key_makes_pixels_transparent_only_when_asked()
    {
        var pixels = new uint[] { Red, Magenta, Magenta, Blue };
        using (var keyed = new Rig("[Button.PushButton]\nBgType = ImageFile\nImageFile = key.bmp\nSizingType = Stretch\nTransparent = True", Image("key.bmp", 2, 2, pixels), 2, 2))
        using (var drawn = keyed.Draw(1, new RectangleF(0, 0, 2, 2)))
        {
            Assert.Equal(Red, Pixel(drawn, 0, 0));
            Assert.Equal(0u, Pixel(drawn, 1, 0));
            Assert.Equal(Blue, Pixel(drawn, 1, 1));
        }

        using var plain = new Rig("[Button.PushButton]\nBgType = ImageFile\nImageFile = key.bmp\nSizingType = Stretch", Image("key.bmp", 2, 2, pixels), 2, 2);
        using var opaque = plain.Draw(1, new RectangleF(0, 0, 2, 2));
        Assert.Equal(Magenta, Pixel(opaque, 1, 0));
    }

    [Fact]
    public void A_custom_key_color_is_honored()
    {
        var pixels = new uint[] { Red, Gray, Gray, Blue };
        using var rig = new Rig("[Button.PushButton]\nBgType = ImageFile\nImageFile = key.bmp\nSizingType = Stretch\nTransparent = True\nTransparentColor = 128 128 128", Image("key.bmp", 2, 2, pixels), 2, 2);
        using var drawn = rig.Draw(1, new RectangleF(0, 0, 2, 2));

        Assert.Equal(0u, Pixel(drawn, 1, 0));
        Assert.Equal(Red, Pixel(drawn, 0, 0));
    }

    [Fact]
    public void A_32bpp_image_with_alpha_is_premultiplied_once()
    {
        var pixels = new uint[] { 0x80ff0000, 0xff00ff00, 0x00000000, 0xff0000ff };
        using var rig = new Rig("[Button.PushButton]\nBgType = ImageFile\nImageFile = alpha.bmp\nSizingType = Stretch\nTransparent = True", Image("alpha.bmp", 2, 2, pixels, alpha: true), 2, 2);
        using var drawn = rig.Draw(1, new RectangleF(0, 0, 2, 2));

        var bitmap = rig.Images[0];
        Assert.True(bitmap.HasAlpha);
        Assert.Equal(0x80800000u, bitmap.PixelAt(0, 0));
        Assert.Equal(0x80ff0000u, Pixel(drawn, 0, 0));
        Assert.Equal(Green, Pixel(drawn, 1, 0));
        Assert.Equal(0u, Pixel(drawn, 0, 1));
    }

    [Fact]
    public void Images_are_selected_by_dpi_with_windows_rule()
    {
        var theme = MsStylesTheme.Parse(MsStylesWriter.Plain());
        var window = theme.OpenClass("Window")!;
        var images = new MsStylesImageCache();
        var bounds = new RectangleF(0, 0, 64, 64);

        Assert.True(MsStylesDrawing.TrySelectImage(window, (int)WindowPart.CloseButton, 1, bounds, glyph: true, new MsStylesDrawContext(images, 96), out var small, out var smallDpi));
        Assert.Equal("BLUE_CLOSEGLYPH_BMP", small.Name);
        Assert.Equal(96, smallDpi);
        Assert.True(MsStylesDrawing.TrySelectImage(window, (int)WindowPart.CloseButton, 1, bounds, glyph: true, new MsStylesDrawContext(images, 144), out var middle, out var middleDpi));
        Assert.Equal("BLUE_CLOSEGLYPH_BMP", middle.Name);
        Assert.Equal(96, middleDpi);
        Assert.True(MsStylesDrawing.TrySelectImage(window, (int)WindowPart.CloseButton, 1, bounds, glyph: true, new MsStylesDrawContext(images, 192), out var large, out var largeDpi));
        Assert.Equal("BLUE_CLOSEGLYPH16_BMP", large.Name);
        Assert.Equal(192, largeDpi);
        Assert.True(MsStylesDrawing.TrySelectImage(window, (int)WindowPart.CloseButton, 1, bounds, glyph: false, new MsStylesDrawContext(images, 192), out var background, out _));
        Assert.Equal("BLUE_CLOSEBUTTON_BMP", background.Name);
    }

    [Fact]
    public void Images_are_selected_by_size_against_the_frame_size()
    {
        var bitmaps = new Dictionary<string, byte[]>
        {
            ["s.bmp"] = MsStylesWriter.Dib(4, 8, Enumerable.Repeat(Red, 32).ToArray(), alpha: false),
            ["l.bmp"] = MsStylesWriter.Dib(8, 16, Enumerable.Repeat(Blue, 128).ToArray(), alpha: false),
        };
        using var rig = new Rig("[Button.PushButton]\nImageSelectType = Size\nImageCount = 2\nImageLayout = Vertical\nImageFile1 = s.bmp\nImageFile2 = l.bmp", bitmaps);
        var context = rig.Context();

        Assert.True(MsStylesDrawing.TrySelectImage(rig.Class, 1, 1, new RectangleF(0, 0, 7, 7), glyph: true, context, out var fits, out _));
        Assert.Equal("S_BMP", fits.Name);
        Assert.True(MsStylesDrawing.TrySelectImage(rig.Class, 1, 1, new RectangleF(0, 0, 8, 8), glyph: true, context, out var larger, out var dpi));
        Assert.Equal("L_BMP", larger.Name);
        Assert.Equal(96, dpi);
        Assert.True(MsStylesDrawing.TrySelectImage(rig.Class, 1, 1, new RectangleF(0, 0, 2, 2), glyph: true, context, out var none, out _));
        Assert.Equal("S_BMP", none.Name);
    }

    [Theory]
    [InlineData(0, 0, Red)]
    [InlineData(1, 0, Red)]
    [InlineData(2, 0, Blue)]
    [InlineData(5, 0, Blue)]
    [InlineData(1, 2, Blue)]
    public void The_frame_index_is_the_state_clamped_to_the_image_count(int state, int frame, uint expected)
    {
        var pixels = new uint[] { Red, Blue };
        using var rig = new Rig("[Button.PushButton]\nBgType = ImageFile\nImageFile = f.bmp\nImageCount = 2\nImageLayout = Vertical\nSizingType = Stretch", Image("f.bmp", 1, 2, pixels), 2, 2);
        using var drawn = rig.Draw(state, new RectangleF(0, 0, 2, 2), frame: frame);

        Assert.Equal(expected, Pixel(drawn, 1, 1));
    }

    [Fact]
    public void A_gradient_honors_its_ratios()
    {
        using var rig = new Rig("[Button.PushButton]\nBgType = BorderFill\nBorderSize = 0\nFillType = VertGradient\nGradientColor1 = 0 0 0\nGradientColor2 = 255 255 255\nGradientRatio1 = 0\nGradientRatio2 = 128", width: 4, height: 64);
        using var drawn = rig.Draw(1, new RectangleF(0, 0, 4, 64));

        Assert.True(Channel(Pixel(drawn, 1, 0), 16) < 8);
        Assert.InRange(Channel(Pixel(drawn, 1, 16), 16), 100, 156);
        Assert.Equal(White, Pixel(drawn, 1, 48));
        Assert.Equal(White, Pixel(drawn, 1, 63));
    }

    [Fact]
    public void A_radial_fill_draws_from_center_to_edge()
    {
        using var rig = new Rig("[Button.PushButton]\nBgType = BorderFill\nBorderSize = 0\nFillType = RadialGradient\nGradientColor1 = 255 0 0\nGradientColor2 = 0 0 255", width: 32, height: 32);
        using var drawn = rig.Draw(1, new RectangleF(0, 0, 32, 32));

        Assert.True(Channel(Pixel(drawn, 16, 16), 16) > 240);
        Assert.True(Channel(Pixel(drawn, 0, 0), 0) > 240);
    }

    [Fact]
    public void A_border_fill_draws_the_border_inside_and_fills_within()
    {
        using var rig = new Rig("[Button.PushButton]\nBgType = BorderFill\nBorderSize = 2\nBorderColor = 255 0 0\nFillColor = 0 0 255");
        using var drawn = rig.Draw(1, new RectangleF(0, 0, 10, 10));

        Assert.Equal(Red, Pixel(drawn, 0, 0));
        Assert.Equal(Red, Pixel(drawn, 1, 5));
        Assert.Equal(Blue, Pixel(drawn, 2, 2));
        Assert.Equal(Blue, Pixel(drawn, 7, 7));
        Assert.Equal(Red, Pixel(drawn, 9, 9));
    }

    [Fact]
    public void An_image_glyph_is_centered_in_the_content_rectangle()
    {
        var glyph = new uint[16];
        Array.Fill(glyph, Yellow);
        var bitmaps = new Dictionary<string, byte[]>
        {
            ["bg.bmp"] = MsStylesWriter.Dib(1, 1, [Blue], alpha: false),
            ["g.bmp"] = MsStylesWriter.Dib(4, 4, glyph, alpha: false),
        };
        using var rig = new Rig("[Button.PushButton]\nBgType = ImageFile\nImageFile = bg.bmp\nSizingType = Stretch\nContentMargins = 2, 0, 0, 0\nGlyphType = ImageGlyph\nGlyphImageFile = g.bmp", bitmaps, 12, 10);
        using var drawn = rig.Draw(1, new RectangleF(0, 0, 12, 10));

        Assert.Equal(Blue, Pixel(drawn, 4, 2));
        Assert.Equal(Yellow, Pixel(drawn, 5, 3));
        Assert.Equal(Yellow, Pixel(drawn, 8, 6));
        Assert.Equal(Blue, Pixel(drawn, 9, 6));
    }

    [Fact]
    public void The_content_rectangle_follows_uxtheme()
    {
        using var content = new Rig("[Button.PushButton]\nBgType = ImageFile\nContentMargins = 1, 2, 3, 4\nSizingMargins = 9, 9, 9, 9");
        Assert.Equal(RectangleF.FromLTRB(1, 3, 98, 96), MsStylesDrawing.ContentRect(content.Class, 1, 1, new RectangleF(0, 0, 100, 100), 96));
        Assert.Equal(RectangleF.FromLTRB(2, 6, 96, 92), MsStylesDrawing.ContentRect(content.Class, 1, 1, new RectangleF(0, 0, 100, 100), 192));

        using var sizing = new Rig("[Button.PushButton]\nBgType = ImageFile\nSizingMargins = 5, 6, 7, 8");
        Assert.Equal(RectangleF.FromLTRB(5, 7, 94, 92), MsStylesDrawing.ContentRect(sizing.Class, 1, 1, new RectangleF(0, 0, 100, 100), 96));

        using var border = new Rig("[Button.PushButton]\nBgType = BorderFill\nBorderSize = 3");
        Assert.Equal(RectangleF.FromLTRB(3, 3, 97, 97), MsStylesDrawing.ContentRect(border.Class, 1, 1, new RectangleF(0, 0, 100, 100), 96));
    }

    [Fact]
    public void A_96_dpi_image_is_scaled_to_a_192_dpi_target()
    {
        using var rig = new Rig(Stretch, Image(@"test\slice.bmp", 6, 6, NineSlice), 20, 20);
        using var drawn = rig.Draw(1, new RectangleF(0, 0, 20, 20), dpi: 192);

        Assert.Equal(Red, Pixel(drawn, 3, 3));
        Assert.Equal(Cyan, Pixel(drawn, 4, 0));
        Assert.Equal(Gray, Pixel(drawn, 4, 4));
        Assert.Equal(Yellow, Pixel(drawn, 16, 16));
    }

    private static uint Pixel(SKBitmap bitmap, int x, int y) => (uint)bitmap.GetPixel(x, y) is var straight && (straight >> 24) == 0 ? 0 : (uint)bitmap.GetPixel(x, y);

    private static int Channel(uint argb, int shift) => (int)((argb >> shift) & 0xff);
}
