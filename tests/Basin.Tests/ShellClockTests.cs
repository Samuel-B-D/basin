using Basin.Host;
using Xunit;

namespace Basin.Tests;

public sealed class ShellClockTests
{
    [Fact]
    public void Frozen_text_replaces_the_formatted_time_until_it_is_cleared()
    {
        using var host = new CompositorTestHost();
        var shown = new List<string?>();
        using var clock = new ShellClock(host.Loop, now => "live", shown.Add);

        clock.Refresh();
        clock.Frozen = "12:00";
        clock.Frozen = null;

        Assert.Equal(["live", "12:00", "live"], shown);
    }

    [Fact]
    public void A_disposed_clock_shows_nothing()
    {
        using var host = new CompositorTestHost();
        var shown = new List<string?>();
        var clock = new ShellClock(host.Loop, now => "live", shown.Add);

        clock.Dispose();
        clock.Refresh();

        Assert.Empty(shown);
    }
}
