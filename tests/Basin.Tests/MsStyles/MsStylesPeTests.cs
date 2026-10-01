using System.Buffers.Binary;
using System.Text;
using Basin.Themes.MsStyles;
using Xunit;

namespace Basin.Tests;

public sealed class MsStylesPeTests
{
    private static byte[] Sample() => new PeImageWriter()
        .Add("PACKTHEM_VERSION", 1, [3, 0])
        .Add("TEXTFILE", "THEMES_INI", Encoding.Unicode.GetBytes("[documentation]\r\n"))
        .Add("IMAGE", 7, [1, 2, 3, 4, 5])
        .Add(PeResources.BitmapType, "BLUE_CAPTION_BMP", [9, 9])
        .AddStrings(new Dictionary<int, string> { [501] = "Segoe UI, 9", [5000] = "Plain", [5003] = "basin" })
        .Build();

    [Fact]
    public void Walks_string_and_integer_types_and_names()
    {
        var resources = PeResources.Read(Sample());

        Assert.Contains(new PeResourceKey("PACKTHEM_VERSION"), resources.Types);
        Assert.Contains(new PeResourceKey(PeResources.BitmapType), resources.Types);
        Assert.True(resources.TryFind("packthem_version", 1, out var version));
        Assert.Equal(new byte[] { 3, 0 }, version.ToArray());
        Assert.True(resources.TryFind("TEXTFILE", "themes_ini", out var ini));
        Assert.Equal("[documentation]\r\n", Encoding.Unicode.GetString(ini.Span));
        Assert.True(resources.TryFind("IMAGE", 7, out var image));
        Assert.Equal(5, image.Length);
        Assert.True(resources.TryFind(PeResources.BitmapType, "BLUE_CAPTION_BMP", out _));
        Assert.False(resources.TryFind("IMAGE", 8, out _));
        Assert.False(resources.TryFind("IMAGE", "7", out _));
        Assert.Equal([new PeResourceKey(7)], resources.Names("IMAGE"));
    }

    [Fact]
    public void Reads_string_table_blocks()
    {
        var resources = PeResources.Read(Sample());

        Assert.True(resources.TryGetString(501, out var font));
        Assert.Equal("Segoe UI, 9", font);
        Assert.True(resources.TryGetString(5000, out var name));
        Assert.Equal("Plain", name);
        Assert.True(resources.TryGetString(5003, out var author));
        Assert.Equal("basin", author);
        Assert.False(resources.TryGetString(5001, out _));
        Assert.False(resources.TryGetString(42, out _));
    }

    [Fact]
    public void Rejects_a_file_that_is_not_a_PE_image()
    {
        var error = Assert.Throws<MsStylesFormatException>(() => PeResources.Read(new byte[64]));
        Assert.Contains("PE", error.Message);
    }

    [Fact]
    public void Rejects_an_offset_out_of_range_with_its_offset()
    {
        var image = Sample();
        var entry = PeImageWriter.ResourceFileOffset + 16;
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(entry + 4), 0x8000_f000);

        var error = Assert.Throws<MsStylesFormatException>(() => PeResources.Read(image));
        Assert.Equal(PeImageWriter.ResourceFileOffset + 0xf000, error.Offset);
        Assert.Contains("out of range", error.Message);
    }

    [Fact]
    public void Rejects_a_directory_cycle()
    {
        var image = Sample();
        var entry = PeImageWriter.ResourceFileOffset + 16;
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(entry + 4), 0x8000_0000);

        var error = Assert.Throws<MsStylesFormatException>(() => PeResources.Read(image));
        Assert.Equal(PeImageWriter.ResourceFileOffset, error.Offset);
        Assert.Contains("cycle", error.Message);
    }

    [Fact]
    public void Rejects_a_tree_with_too_many_entries()
    {
        var image = Sample();
        var root = PeImageWriter.ResourceFileOffset;
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(root + 12), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(root + 14), 0xffff);

        var error = Assert.Throws<MsStylesFormatException>(() => PeResources.Read(image));
        Assert.Equal(root, error.Offset);
        Assert.Contains("65536", error.Message);
    }
}
