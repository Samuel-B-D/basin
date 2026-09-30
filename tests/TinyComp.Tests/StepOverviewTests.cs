using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Basin.Diagnostics;
using Basin.Effects;
using Xunit;
using static TinyComp.Tests.CanvasRows;

namespace TinyComp.Tests;

public sealed class StepOverviewTests
{
    [Fact]
    public void A_step_drag_across_the_wall_changes_scale_smoothly_and_unshelves_on_the_drop()
    {
        using var session = CanvasSession.Start(StepConfig, SsdWin());
        Assert.SkipWhen(session is null, "tinycomp or ssdwin is not available beside the tests");
        session!.Send("overview open");
        Assert.NotNull(session.WaitForLine("OVERVIEW output=HEADLESS-1 open=true progress=1.00", 5000));
        session.Send("shelve right");
        _ = session.WaitForLine("SHELVE ");
        _ = session.Shot("shelved");
        var (_, _, screen, _, _) = session.CanvasWindow();
        var grabX = screen.X + 20;
        var grabY = screen.Y - 5;
        session.Send($"move {grabX} {grabY}");
        session.Send("button 272 1");
        var scales = new List<double>();
        foreach (var x in new[] { 1190, 1175, 1165, 1155, 1145, 1135, 1125, 1115, 1000, 900 })
        {
            session.Send($"move {x} {grabY}");
            scales.Add(session.CanvasScale());
        }

        for (var i = 1; i < scales.Count; i++)
        {
            Assert.True(scales[i] >= scales[i - 1] - 0.001, $"the scale fell from {scales[i - 1]} to {scales[i]}");
            Assert.True(scales[i] - scales[i - 1] < 0.12, $"the scale jumped from {scales[i - 1]} to {scales[i]}");
        }

        Assert.Equal(0.75, scales[^1], 2);
        session.Send("button 272 0");
        Assert.NotNull(session.WaitForLine("UNSHELVE "));
        _ = session.Shot("unshelved");
        var back = OverviewWindow(session);
        Assert.Equal("false", back["shelved"]);
        Assert.Equal("flat", back["region"]);
        Assert.Equal("0.75", back["k"]);
    }

    [Fact]
    public void A_step_drag_into_a_corner_rests_there_as_one_uniform_scale()
    {
        using var session = CanvasSession.Start(StepFourConfig, SsdWin());
        Assert.SkipWhen(session is null, "tinycomp or ssdwin is not available beside the tests");
        session!.Send("overview open");
        var line = session.WaitForLine("OVERVIEW output=HEADLESS-1 open=true progress=1.00", 5000);
        Assert.Contains("sides=left,right,top,bottom", line);
        _ = session.Shot("open");
        var (_, _, screen, _, _) = session.CanvasWindow();
        var grabX = screen.X + 20;
        var grabY = screen.Y - 5;
        session.Send($"move {grabX} {grabY}");
        session.Send("button 272 1");
        for (var step = 1; step <= 10; step++)
        {
            session.Send($"move {grabX + ((1265 - grabX) * step / 10)} {grabY + ((20 - grabY) * step / 10)}");
        }

        session.Send("button 272 0");
        Assert.Contains("held=in", session.WaitForLine("OVERVIEW held="));
        Assert.Contains("side=right-top", session.WaitForLine("SHELVE "));
        _ = session.Shot("corner");
        var corner = OverviewWindow(session);
        Assert.Equal("corner", corner["region"]);
        var (_, _, drawn, _, _) = session.CanvasWindow();
        Assert.True(drawn.X >= 1171 - 1 && drawn.Right <= 1281, $"the corner window is drawn at {drawn}");
        Assert.True(drawn.Y >= -1 && drawn.Bottom <= 62, $"the corner window is drawn at {drawn}");
    }

    [Fact]
    public void A_click_on_a_step_wall_closes_overview()
    {
        using var session = CanvasSession.Start(StepConfig, SsdWin());
        Assert.SkipWhen(session is null, "tinycomp or ssdwin is not available beside the tests");
        session!.Send("overview open");
        Assert.NotNull(session.WaitForLine("OVERVIEW output=HEADLESS-1 open=true progress=1.00", 5000));
        _ = session.Shot("open");
        session.Send("move 1145 600");
        session.Send("button 272 1");
        session.Send("button 272 0");
        Assert.NotNull(session.WaitForLine("OVERVIEW output=HEADLESS-1 open=false progress=0.00", 5000));
        Assert.Null(session.WaitForClientLine("BUTTON ", 500));
    }

    [Fact]
    public void A_step_shelf_resize_toward_the_desktop_stops_at_the_base_line()
    {
        using var session = CanvasSession.Start(StepConfig, SsdWin());
        Assert.SkipWhen(session is null, "tinycomp or ssdwin is not available beside the tests");
        session!.Send("overview open");
        Assert.NotNull(session.WaitForLine("OVERVIEW output=HEADLESS-1 open=true progress=1.00", 5000));
        session.Send("shelve right");
        _ = session.WaitForLine("SHELVE ");
        _ = session.Shot("shelved");
        var (_, _, shelf, _, _) = session.CanvasWindow();
        var shelfY = shelf.Y + (shelf.Height / 2);
        session.Send($"move {shelf.X - 4} {shelfY}");
        session.Send("button 272 1");
        for (var x = shelf.X - 10; x > 1000; x -= 15)
        {
            session.Send($"move {x} {shelfY}");
        }

        var held = session.CanvasFields();
        session.Send("button 272 0");
        _ = session.Shot("released");
        var released = session.CanvasFields();
        Assert.Equal("shelf", released["region"]);
        Assert.InRange(int.Parse(released["screen"].Split(',')[0], CultureInfo.InvariantCulture), 1170, 1200);
        Assert.InRange(
            int.Parse(released["width"], CultureInfo.InvariantCulture),
            int.Parse(held["width"], CultureInfo.InvariantCulture) - 2,
            int.Parse(held["width"], CultureInfo.InvariantCulture) + 2);
    }

    [Fact]
    public void Overview_is_refused_on_an_output_with_the_canvas_enabled()
    {
        using var session = CanvasSession.Start("[canvas]\nenable = true\ngrid = \"never\"\n", SsdWin());
        Assert.SkipWhen(session is null, "tinycomp or ssdwin is not available beside the tests");
        session!.Send("overview open");
        Assert.Equal("OVERVIEW refused: canvas", session.WaitForLine("OVERVIEW "));
        session.Send("shelve right");
        Assert.Equal("SHELVE refused: canvas", session.WaitForLine("SHELVE "));
    }
}
