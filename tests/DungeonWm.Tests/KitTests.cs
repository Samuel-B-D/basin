using System.Diagnostics;
using Xunit;
using Basin.Scene3D.Gltf;

namespace DungeonWm.Tests;

public sealed class KitTests
{
    private readonly ITestOutputHelper _output;

    public KitTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Every_manifest_row_loads_and_shares_one_atlas()
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        var clock = Stopwatch.StartNew();
        var kit = Kit.Load();
        clock.Stop();
        var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        _output.WriteLine($"kit: {KitManifest.Pieces.Length} pieces in {clock.ElapsedMilliseconds} ms, {bytes / 1024} KB");

        foreach (var piece in KitManifest.Pieces)
        {
            var model = kit[piece.Name];
            Assert.True(model.Mesh.TriangleCount > 0, piece.Name);
            Assert.False(model.Bounds.IsEmpty, piece.Name);
            Assert.Same(kit.Images[0], model.Mesh.Image);
        }

        Assert.Single(kit.Images);
        Assert.Equal((1024, 1024), (kit.Images[0].Width, kit.Images[0].Height));
    }

    [Theory]
    [InlineData(0, 0, 89, 96, 100)]
    [InlineData(100, 200, 64, 70, 74)]
    [InlineData(512, 512, 254, 215, 103)]
    [InlineData(1000, 37, 128, 112, 99)]
    [InlineData(1023, 1023, 30, 30, 28)]
    public void The_atlas_decodes_to_the_reference_pixels(int x, int y, byte r, byte g, byte b)
    {
        var image = Kit.Load().Images[0];
        var at = ((y * image.Width) + x) * 4;
        Assert.Equal((r, g, b, (byte)255), (image.Rgba[at], image.Rgba[at + 1], image.Rgba[at + 2], image.Rgba[at + 3]));
    }

    [Fact]
    public void The_wall_module_is_four_units_by_four_by_one()
    {
        var bounds = Kit.Load()["wall"].Bounds;
        Assert.Equal(4f, bounds.Size.X, 2);
        Assert.Equal(4f, bounds.Size.Y, 2);
        Assert.Equal(1f, bounds.Size.Z, 2);
    }

    [Fact]
    public void The_doorway_loads_without_its_door()
    {
        var kit = Kit.Load();
        var document = GlbDocument.Parse(Kit.ReadResource(KitManifest.Find("wall_doorway").Resource));
        var whole = GlbMeshes.Flatten(document, [], null);
        Assert.True(kit["wall_doorway"].Mesh.TriangleCount < whole.TriangleCount);
    }

    [Fact]
    public void The_avatar_loads_with_its_skin_and_clips()
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        var clock = Stopwatch.StartNew();
        var document = GlbDocument.Parse(Kit.ReadResource(KitManifest.Avatar));
        var image = Kit.DecodeImage(document.ReadImage(0));
        clock.Stop();
        var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        _output.WriteLine($"avatar: {clock.ElapsedMilliseconds} ms, {bytes / 1024} KB");

        Assert.Single(document.Root.Skins!);
        Assert.Equal(41, document.Root.Skins![0].Joints!.Length);
        var clips = document.Root.Animations!.Select(a => a.Name).ToHashSet();
        foreach (var clip in new[] { "Idle", "Walking_A", "Running_A", "Jump_Start", "Jump_Idle", "Jump_Land", "PickUp", "Throw", "Interact" })
        {
            Assert.Contains(clip, clips);
        }

        Assert.Equal(1024, image.Width);
    }
}
