using Basin.Backend.Wayland;
using Basin.Backend.Wayland.Skia;
using Basin.Capabilities;
using Basin.UI.Skia;
using Xunit;

namespace Basin.Tests;

public class NestedHostChromeTests
{
    private static void Map(NestedBackendTestHost host, WaylandOutput output)
    {
        var content = new MemoryBuffer(output.CurrentMode.Width, output.CurrentMode.Height, DrmFormat.Xrgb8888);
        using (var state = new OutputState())
        {
            Assert.True(output.Commit(state.SetBuffer(content)));
        }

        host.Pump();
    }

    private static int BandsWithContent(NestedBackendTestHost host) => host.Parent.Invoke(() =>
    {
        var surface = host.Parent.Toplevels[0].Xdg.Surface;
        return surface.SubsurfacesBelow.Concat(surface.SubsurfacesAbove).Count(s => s.Surface.Current.Buffer is not null);
    });

    [Fact]
    public void The_default_chrome_frames_an_undecorated_output_with_a_titlebar()
    {
        using var host = new NestedBackendTestHost(NestedParentOptions.Undecorating);
        var output = host.CreateOutput();
        using var wiring = DefaultHostChrome.Wire(output);
        Map(host, output);

        var insets = output.HostFrame!.Insets;
        Assert.Equal(new HostFrameInsets(30, 4, 4, 4), insets);
        Assert.Equal(4, BandsWithContent(host));
        var geometry = host.Parent.Invoke(() => host.Parent.Toplevels[0].Xdg.WindowGeometry);
        Assert.Equal(-insets.Left, geometry.X);
        Assert.Equal(-insets.Top, geometry.Y);
    }

    [Fact]
    public void The_default_chrome_stays_away_from_a_parent_that_decorates()
    {
        using var host = new NestedBackendTestHost(NestedParentOptions.Decorating);
        var output = host.CreateOutput();
        using var wiring = DefaultHostChrome.Wire(output);
        Map(host, output);

        Assert.Null(output.HostFrame);
        Assert.Equal(800, output.CurrentMode.Width);
    }

    [Fact]
    public void The_close_button_closes_the_output_and_the_title_is_the_outputs()
    {
        using var host = new NestedBackendTestHost(NestedParentOptions.Undecorating);
        var output = host.CreateOutput();
        var frame = output.HostFrame!;
        using var theme = FlatFrameTheme.Default();
        using var uiHost = new SkiaUIHost();
        var renderer = new FlatFrameRenderer(theme);
        using var chrome = new FrameHostChrome(frame, uiHost, renderer);
        output.Committed += chrome.OnOutputChanged;
        Map(host, output);
        output.SetTitle("dungeon-wm");
        var closed = 0;
        output.CloseRequested += () => closed++;

        var close = renderer.PartBounds(FramePart.Close);
        Assert.False(close.IsEmpty);
        var (x, y) = (close.X + (close.Width / 2.0), close.Y + (close.Height / 2.0));
        frame.OnPointerEnter(x, y);
        frame.OnPointerButton(10, InputCodes.BtnLeft, true);
        Assert.Equal(0, closed);
        frame.OnPointerButton(20, InputCodes.BtnLeft, false);

        Assert.Equal(1, closed);
        Assert.Equal("dungeon-wm", frame.Title);
        Assert.Null(chrome.FixedTitle);
        Assert.False(chrome.Faulted);
    }

    [Fact]
    public void A_press_on_the_title_starts_a_move_and_a_second_one_maximizes()
    {
        using var host = new NestedBackendTestHost(NestedParentOptions.Undecorating);
        var output = host.CreateOutput();
        var frame = output.HostFrame!;
        using var theme = FlatFrameTheme.Default();
        using var uiHost = new SkiaUIHost();
        using var chrome = new FrameHostChrome(frame, uiHost, new FlatFrameRenderer(theme), "basin");
        output.Committed += chrome.OnOutputChanged;
        Map(host, output);

        frame.OnPointerEnter(200, 12);
        frame.OnPointerButton(1000, InputCodes.BtnLeft, true);
        frame.OnPointerButton(1050, InputCodes.BtnLeft, false);
        frame.OnPointerButton(1200, InputCodes.BtnLeft, true);
        host.Pump();

        Assert.True(host.Parent.Invoke(() => host.Parent.Toplevels[0].RequestedMaximized));
    }
}
