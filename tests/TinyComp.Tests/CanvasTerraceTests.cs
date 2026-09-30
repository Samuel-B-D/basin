using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Basin.Diagnostics;
using Basin.Effects;
using Xunit;
using static TinyComp.Tests.CanvasRows;

namespace TinyComp.Tests;

public sealed class CanvasTerraceTests
{
    [Fact]
    public void A_shelf_step_on_a_warp_output_is_refused()
    {
        using var session = CanvasSession.Start();
        Assert.SkipWhen(session is null, "tinycomp or weston-simple-shm is not available beside the tests");
        session!.Send("shelf larger");
        Assert.Equal("SHELF view=0 refused: window=warp", session.WaitForLine("SHELF "));
        session.Send("canvas mode");
        Assert.Equal("CANVASMODE view=0 window=scale", session.WaitForLine("CANVASMODE "));
        session.Send("canvas mode");
        Assert.Equal("CANVASMODE view=0 window=terrace", session.WaitForLine("CANVASMODE "));
        session.Send("canvas mode");
        Assert.Equal("CANVASMODE view=0 window=warp", session.WaitForLine("CANVASMODE "));
    }

    [Fact]
    public void A_click_on_a_shelf_window_reaches_the_client_at_true_surface_coordinates()
    {
        using var session = CanvasSession.Start(
            "[canvas]\nenable = true\ngrid = \"never\"\nanimation_ms = 100\nwindow = \"terrace\"\n", SsdWin());
        Assert.SkipWhen(session is null, "tinycomp or ssdwin is not available beside the tests");
        session!.Send("park right");
        session.WaitForLine("PARK ");
        _ = session.Shot("shelf");
        var (_, _, screen, _, _) = session.CanvasWindow();
        var scale = session.CanvasScale();
        Assert.InRange(scale, 0.3, 0.5);
        foreach (var (fx, fy) in new[] { (0.5, 0.5), (0.2, 0.8), (0.9, 0.3) })
        {
            var x = Math.Round(screen.X + (screen.Width * fx), 2);
            var y = Math.Round(screen.Y + (screen.Height * fy), 2);
            session.Send(string.Create(CultureInfo.InvariantCulture, $"move {x} {y}"));
            session.Send("button 272 1");
            session.Send("button 272 0");
            var line = session.WaitForClientLine("BUTTON ");
            Assert.NotNull(line);
            var parts = line!.Split(' ');
            var localX = double.Parse(parts[2], CultureInfo.InvariantCulture);
            var localY = double.Parse(parts[3], CultureInfo.InvariantCulture);
            Assert.True(
                Math.Abs(localX - ((x - screen.X) / scale)) < 4.0 && Math.Abs(localY - ((y - screen.Y) / scale)) < 4.0,
                $"a click at ({x},{y}) reached ({localX},{localY}) on a window drawn at {screen} and {scale}");
        }
    }
}
