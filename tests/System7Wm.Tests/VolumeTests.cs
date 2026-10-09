using Xunit;

namespace System7Wm.Tests;

public sealed class VolumeTests
{
    [Fact]
    public async Task The_volume_reaches_the_default_sink_of_a_private_pipewire()
    {
        using var pipewire = PrivatePipeWire.Start();
        var environment = new Dictionary<string, string>
        {
            ["BASIN_MUTE"] = "",
            ["PIPEWIRE_REMOTE"] = pipewire.Socket,
        };
        using var session = new Session(Prerequisites.Compositor(), "pixman", environment);
        await session.DisplayAsync();

        await session.SendAsync("volume");
        var before = await session.WaitForPrefixAsync("SINK ");
        Assert.StartsWith($"SINK {PrivatePipeWire.SinkName} level=7 volume=1.000 muted=false route=false", before, StringComparison.Ordinal);

        var mark = session.Mark();
        await session.SendAsync("volume 3");
        Assert.NotNull(await session.WaitForLineAsync("VOLUME 3", since: mark));
        mark = session.Mark();
        await session.SendAsync("volume");
        var after = await session.WaitForPrefixAsync("SINK ", since: mark);
        Assert.Equal($"SINK {PrivatePipeWire.SinkName} level=3 volume=0.429 muted=false route=false", after);

        mark = session.Mark();
        await session.SendAsync("panel Sound");
        Assert.NotNull(await session.WaitForLineAsync("PANEL Sound open", since: mark));
        await session.QuitCleanlyAsync();
    }

    [Fact]
    public async Task Under_basin_mute_the_volume_prints_and_touches_no_sink()
    {
        using var pipewire = PrivatePipeWire.Start();
        using var session = new Session(Prerequisites.Compositor(), "pixman", new Dictionary<string, string> { ["PIPEWIRE_REMOTE"] = pipewire.Socket });
        await session.DisplayAsync();

        await session.SendAsync("volume 5");
        Assert.NotNull(await session.WaitForLineAsync("VOLUME 5"));
        var mark = session.Mark();
        await session.SendAsync("volume");
        Assert.Equal("SINK none level=5", await session.WaitForPrefixAsync("SINK ", since: mark));
        await session.QuitCleanlyAsync();
    }
}
