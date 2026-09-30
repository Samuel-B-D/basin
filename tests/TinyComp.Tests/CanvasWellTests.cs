using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Basin.Diagnostics;
using Basin.Effects;
using Xunit;
using static TinyComp.Tests.CanvasRows;

namespace TinyComp.Tests;

public sealed class CanvasWellTests
{
    [Theory]
    [InlineData(0.1)]
    [InlineData(0.5)]
    [InlineData(0.9)]
    public void A_window_dragged_into_the_well_reaches_its_smallest_size_at_the_screen_edge_wherever_it_was_grabbed(double grab)
    {
        using var session = CanvasSession.Start(ScaleMode, SsdWin());
        Assert.SkipWhen(session is null, "tinycomp or ssdwin is not available beside the tests");
        var (_, _, start, _, _) = session!.CanvasWindow();
        var grabX = start.X + (int)(grab * start.Width);
        var y = start.Y + 80;
        session.Send($"move {grabX} {y}");
        session.Send("key 56 1");
        session.Send("button 272 1");
        var previous = 1.0;
        var floored = false;
        for (var x = grabX + 20; x < 1280 + 20; x += 20)
        {
            var cursor = Math.Min(x, 1279);
            session.Send($"move {cursor} {y}");
            var (_, _, drawn, _, _) = session.CanvasWindow();
            var scale = session.CanvasScale();
            Assert.True(Math.Abs(((cursor - drawn.X) / (double)drawn.Width) - grab) < 0.03, $"the grabbed point left the cursor at {cursor}: {drawn}");
            Assert.True(scale <= previous + 0.005 && previous - scale < 0.2, $"the scale jumped from {previous} to {scale} at {cursor}");
            if (scale > 0.36)
            {
                Assert.True(drawn.Right <= 1281, $"the window left the screen before its smallest size at {cursor}: {drawn}");
            }
            else if (!floored)
            {
                floored = true;
                Assert.True(drawn.Right >= 1280 - 24 && drawn.Right <= 1280 + 24, $"the smallest size arrived away from the edge at {cursor}: {drawn}");
            }

            previous = scale;
        }

        var (_, _, before, _, _) = session.CanvasWindow();
        session.Send("button 272 0");
        session.Send("key 56 0");
        Thread.Sleep(500);
        var (_, _, after, _, _) = session.CanvasWindow();
        Assert.True(floored, "the window reached its smallest size");
        Assert.Equal(0.35, session.CanvasScale(), 2);
        Assert.True(Math.Abs(after.X - before.X) <= 2 && Math.Abs(after.Y - before.Y) <= 2, $"the drop moved the window from {before} to {after}");
    }
}
