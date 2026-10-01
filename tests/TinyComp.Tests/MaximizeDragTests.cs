using Xunit;

namespace TinyComp.Tests;

public sealed class MaximizeDragTests
{
    private const string Config = "[frame]\nstyle = \"flat\"\n\n[overview]\nhot_corner = \"none\"\n";

    [Fact]
    public void Dragging_a_maximized_title_restores_the_window_under_the_pointer()
    {
        using var session = QuillFrameTests.QuillSession.Start("pixman", Config);
        Assert.SkipWhen(session is null, "tinycomp or ssdwin is not available beside the tests");

        var (x, y) = session!.WindowPosition();
        var titleX = x + 40;
        var titleY = y - 8;
        session.Send($"move {titleX} {titleY}");
        session.Send("button 272 1");
        session.Send("button 272 0");
        session.Send("button 272 1");
        session.Send("button 272 0");
        Thread.Sleep(600);

        var (maxX, maxY) = session.WindowPosition();
        Assert.True(maxX < x && maxY < y, $"the double press did not maximize the window: ({x},{y}) -> ({maxX},{maxY})");

        var grabX = 300;
        var grabY = maxY - 8;
        session.Send($"move {grabX} {grabY}");
        session.Send("button 272 1");
        session.Send($"move {grabX + 2} {grabY + 1}");
        session.Send("button 272 0");
        Thread.Sleep(300);
        Assert.Equal((maxX, maxY), session.WindowPosition());

        session.Send($"move {grabX} {grabY}");
        session.Send("button 272 1");
        session.Send($"move {grabX} {grabY + 20}");
        session.Send($"move {grabX} {grabY + 100}");
        session.Send($"move {grabX} {grabY + 200}");
        session.Send("button 272 0");
        Thread.Sleep(600);

        var (restoredX, restoredY) = session.WindowPosition();
        Assert.InRange(restoredY - maxY, 180, 220);
        Assert.InRange(grabX - restoredX, 0, 256);
    }
}
