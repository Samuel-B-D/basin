using Basin.Audio.PipeWire;
using Xunit;

namespace DungeonWm.Tests;

public sealed class ReverbTests
{
    [Fact]
    public void Each_region_gets_a_sabine_time_from_its_size_and_a_bigger_room_rings_longer()
    {
        var world = new DungeonWmWorld(Kit.Load(), new DungeonWmSettings());
        for (var i = 0; i < 4; i++)
        {
            world.AddRoom();
        }

        var times = world.Map.Regions.Select(region => (Region: region, Seconds: Acoustics.ReverbSeconds(region, world.Unit, 10))).ToList();
        foreach (var (region, seconds) in times)
        {
            Assert.InRange(seconds, 0.1f, 3f);
            TestContext.Current.SendDiagnosticMessage($"{region.Kind} {region.Id} {region.Width}x{region.Depth} cells={region.Cells.Count} rt60={seconds:F3} unit={world.Unit}");
        }

        var rooms = times.Where(t => t.Region.Kind == RegionKind.Room).OrderBy(t => t.Region.Cells.Count).ToList();
        Assert.True(rooms[^1].Seconds > rooms[0].Seconds, "the largest room rings no longer than the smallest");
        Assert.Equal(0.5f, Acoustics.ReverbSeconds(world.Map.Rooms[0], world.Unit, 0.5f));
    }

    [Fact]
    public void A_distant_source_is_mostly_reverb()
    {
        var settings = new AudioSettings();

        var near = Acoustics.WetDb(0, settings);
        var far = Acoustics.WetDb(-40, settings);

        Assert.Equal(20 * MathF.Log10(settings.Reverb), near, 3);
        Assert.True(far > near + 10, $"near {near} dB, far {far} dB");
        Assert.Equal(VoiceParams.SilentDb, Acoustics.WetDb(VoiceParams.SilentDb, settings));
        Assert.Equal(VoiceParams.SilentDb, Acoustics.WetDb(0, settings with { Reverb = 0 }));
    }
}
