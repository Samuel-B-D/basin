using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Basin.Diagnostics;
using Basin.Effects;
using Xunit;
using static TinyComp.Tests.CanvasRows;

namespace TinyComp.Tests;

public sealed class CanvasDockTests
{
    [Fact]
    public void A_dock_that_maps_late_keeps_a_parked_window_out_from_under_it()
    {
        using var session = CanvasSession.Start(FourSides);
        Assert.SkipWhen(session is null || Panel() is null, "tinycomp, weston-simple-shm or panel is not available beside the tests");
        session!.Send("park left");
        session.WaitForLine("PARK ");
        var before = session.Shot("before");
        Assert.True(Saturated(before, 0, 60) > 0, "the parked client reaches the screen edge before the dock");
        session.Spawn(Panel()!, "left", "60");
        Assert.NotNull(session.WaitForLine("CANVAS view=0"));
        var after = session.Shot("after");
        Assert.Equal(60 + ZoneWidth - 640, session.WindowPosition().X);
        Assert.Equal(0, Saturated(after, 0, 60));
        Assert.True(Saturated(after, 60, ZoneWidth) > 0, "the parked client draws beside the dock");
    }

    [Fact]
    public void Terrace_mode_parks_on_the_shelf_and_a_shelf_change_keeps_the_outer_edge()
    {
        using var session = CanvasSession.Start(clientPath: SsdWin());
        Assert.SkipWhen(session is null, "tinycomp or ssdwin is not available beside the tests");
        session!.Send("canvas mode terrace");
        Assert.Equal("CANVASMODE view=0 window=terrace", session.WaitForLine("CANVASMODE "));
        session.Send("park right");
        Assert.Contains(" scale=0.400", session.WaitForLine("PARK "));
        Thread.Sleep(400);
        var parked = session.CanvasFields();
        Assert.Equal("shelf", parked["region"]);
        Assert.Equal(0.4, double.Parse(parked["k"], CultureInfo.InvariantCulture), 2);
        var (_, _, before, _, home) = session.CanvasWindow();
        Assert.NotEqual("none", home);
        Assert.InRange(before.Right, 1276, 1280);

        session.Send("shelf smaller");
        Assert.Equal("SHELF view=0 side=right scale=0.35 from=0.40", session.WaitForLine("SHELF "));
        Thread.Sleep(400);
        var smaller = session.CanvasFields();
        Assert.Equal("shelf", smaller["region"]);
        Assert.Equal(0.35, double.Parse(smaller["k"], CultureInfo.InvariantCulture), 2);
        var (_, _, after, _, _) = session.CanvasWindow();
        Assert.True(Math.Abs(after.Right - before.Right) <= 2, $"the outer edge moved from {before} to {after}");
        Assert.True(Math.Abs((after.Y + (after.Height / 2)) - (before.Y + (before.Height / 2))) <= 2, $"the center moved from {before} to {after}");
        Assert.True(after.Width < before.Width, $"the window did not shrink: {before} to {after}");

        session.Send("shelf");
        Assert.Equal("SHELF view=0 window=terrace scales=0.40,0.35,0.40,0.40", session.WaitForLine("SHELF "));
        session.Send("shelf reset");
        Assert.Equal("SHELF view=0 side=right scale=0.40 from=0.35", session.WaitForLine("SHELF "));
        session.Send("recall");
        session.WaitForLine("RECALL ");
        Thread.Sleep(400);
        var recalled = session.CanvasFields();
        Assert.Equal(home, recalled["canvas"]);
        Assert.Equal("1.000", recalled["k"]);
    }
}
