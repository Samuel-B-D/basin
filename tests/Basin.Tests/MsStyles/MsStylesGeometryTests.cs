using System.Drawing;
using Basin.Capabilities;
using Basin.Frames.MsStyles;
using Basin.Themes.MsStyles;
using Xunit;

namespace Basin.Tests;

public sealed class MsStylesGeometryTests
{
    [Theory]
    [InlineData("Plain", "active", 24, 4, 4, 4)]
    [InlineData("Plain", "utility", 18, 4, 4, 4)]
    [InlineData("Plain", "maximized", 20, 0, 0, 0)]
    [InlineData("Plain", "border", 4, 4, 4, 4)]
    [InlineData("Glassy", "active", 28, 8, 8, 8)]
    [InlineData("Glassy", "utility", 26, 8, 8, 8)]
    [InlineData("Glassy", "maximized", 20, 0, 0, 0)]
    public void Insets_follow_the_windows_metrics(string theme, string state, int top, int right, int bottom, int left)
    {
        using var rig = new MsStylesRig(theme, theme == "Glassy" ? MsStylesLook.Basic : MsStylesLook.Auto);
        var renderer = rig.Renderer();

        Assert.Equal(new FrameInsets(top, right, bottom, left), renderer.Measure(MsStylesRig.StateFor(state), 1.0));
        Assert.Equal(new FrameInsets(top, right, bottom, left), renderer.Measure(MsStylesRig.StateFor(state), 2.0));
        Assert.Equal(default, renderer.Measure(MsStylesRig.StateFor(state) with { Fullscreen = true }, 1.0));
    }

    [Fact]
    public void Plain_places_buttons_from_the_right_edge_with_two_pixel_gaps()
    {
        using var rig = new MsStylesRig();
        var painter = new MsStylesFramePainter(rig.Style);
        Assert.True(painter.Layout(MsStylesRig.StateFor("active"), 200, 60));
        var geometry = painter.Geometry;

        Assert.Equal(208, geometry.Width);
        Assert.Equal(88, geometry.Height);
        Assert.Equal(
            [(FramePart.Close, new RectangleF(186, 6, 16, 15)), (FramePart.Maximize, new RectangleF(168, 6, 16, 15)), (FramePart.Minimize, new RectangleF(150, 6, 16, 15))],
            geometry.Buttons.ToArray().Select(b => (b.Part, b.Bounds)));
        Assert.Equal(new RectangleF(6, 6, 16, 16), geometry.IconRect);
        Assert.Equal(RectangleF.FromLTRB(26, 4, 147, 24), geometry.TitleRect);
        Assert.Equal(new RectangleF(0, 0, 208, 24), geometry.CaptionRect);
        Assert.Equal(RectangleF.FromLTRB(0, 24, 4, 84), geometry.LeftRect);
        Assert.Equal(RectangleF.FromLTRB(204, 24, 208, 84), geometry.RightRect);
        Assert.Equal(RectangleF.FromLTRB(0, 84, 208, 88), geometry.BottomRect);
    }

    [Fact]
    public void Plain_maximized_keeps_the_buttons_where_the_frame_would_put_them()
    {
        using var rig = new MsStylesRig();
        var painter = new MsStylesFramePainter(rig.Style);
        Assert.True(painter.Layout(MsStylesRig.StateFor("maximized"), 200, 60));
        var geometry = painter.Geometry;

        Assert.Equal(new RectangleF(182, 2, 16, 15), geometry.Buttons[0].Bounds);
        Assert.Equal((int)WindowPart.RestoreButton, geometry.Buttons[1].WindowPart);
        Assert.Equal(new RectangleF(2, 2, 16, 16), geometry.IconRect);
        Assert.Equal(new RectangleF(0, 0, 200, 20), geometry.CaptionRect);
        Assert.True(geometry.LeftRect.IsEmpty);
    }

    [Fact]
    public void Kinds_choose_their_buttons()
    {
        using var rig = new MsStylesRig();
        var painter = new MsStylesFramePainter(rig.Style);

        painter.Layout(MsStylesRig.StateFor("dialog"), 200, 60);
        Assert.Equal([FramePart.Close], painter.Geometry.Buttons.ToArray().Select(b => b.Part));
        Assert.True(painter.Geometry.IconRect.IsEmpty);

        painter.Layout(MsStylesRig.StateFor("utility"), 200, 60);
        var close = Assert.Single(painter.Geometry.Buttons.ToArray());
        Assert.Equal((int)WindowPart.SmallCloseButton, close.WindowPart);
        Assert.Equal(new RectangleF(193, 6, 9, 9), close.Bounds);

        painter.Layout(MsStylesRig.StateFor("no-minimize"), 200, 60);
        Assert.Equal([true, true, false], painter.Geometry.Buttons.ToArray().Select(b => b.Enabled));

        painter.Layout(MsStylesRig.StateFor("active") with { Capabilities = FrameCapabilities.None }, 200, 60);
        Assert.Equal([FramePart.Close], painter.Geometry.Buttons.ToArray().Select(b => b.Part));
    }

    [Fact]
    public void Glassy_basic_uses_the_measured_vista_buttons()
    {
        using var rig = new MsStylesRig("Glassy", MsStylesLook.Basic);
        var painter = new MsStylesFramePainter(rig.Style);
        Assert.True(painter.Layout(MsStylesRig.StateFor("active"), 200, 60));

        Assert.Equal(
            [new RectangleF(178, 10, 28, 15), new RectangleF(148, 10, 28, 15), new RectangleF(118, 10, 28, 15)],
            painter.Geometry.Buttons.ToArray().Select(b => b.Bounds));
        Assert.Equal(new RectangleF(10, 10, 16, 16), painter.Geometry.IconRect);

        painter.Layout(MsStylesRig.StateFor("utility"), 200, 60);
        Assert.Equal(new RectangleF(193, 10, 13, 13), painter.Geometry.Buttons[0].Bounds);
    }

    [Theory]
    [InlineData("Plain", 1.0)]
    [InlineData("Plain", 2.0)]
    [InlineData("Glassy", 1.0)]
    [InlineData("Glassy", 2.0)]
    public void PartAt_agrees_with_PartBounds(string theme, double scale)
    {
        using var rig = new MsStylesRig(theme, theme == "Glassy" ? MsStylesLook.Basic : MsStylesLook.Auto);
        var painter = new MsStylesFramePainter(rig.Style);
        var state = MsStylesRig.StateFor("active");
        painter.Layout(state, 200, 60);
        painter.Paint(state, default, scale);
        foreach (var part in new[] { FramePart.Close, FramePart.Maximize, FramePart.Minimize, FramePart.Menu, FramePart.Title })
        {
            var bounds = painter.Geometry.BoundsOf(part);
            Assert.False(bounds.IsEmpty);
            var x = bounds.X + bounds.Width / 2.0;
            var y = bounds.Y + bounds.Height / 2.0;
            Assert.Equal(part, painter.PartAt(x, y));
            var damage = painter.PartBounds(part);
            Assert.Equal(part is FramePart.Menu or FramePart.Title, damage.IsEmpty);
            if (!damage.IsEmpty)
            {
                Assert.Equal(part, painter.PartAt(damage.X + damage.Width / 2.0, damage.Y + damage.Height / 2.0));
            }
        }

        var width = painter.Geometry.Width;
        var height = painter.Geometry.Height;
        Assert.Equal(FramePart.BottomRight, painter.PartAt(width - 1, height - 1));
        Assert.Equal(FramePart.Bottom, painter.PartAt(width / 2.0, height - 1));
        Assert.Equal(FramePart.Left, painter.PartAt(0, height / 2.0));
        Assert.Equal(FramePart.Top, painter.PartAt(width / 2.0, 1));
        Assert.Equal(FramePart.None, painter.PartAt(-1, 5));
    }

    [Fact]
    public void A_transparent_corner_pixel_passes_the_click_through()
    {
        using var rig = new MsStylesRig();
        var painter = new MsStylesFramePainter(rig.Style);
        var state = MsStylesRig.StateFor("active");
        painter.Layout(state, 200, 60);
        painter.Paint(state, default, 1.0);

        Assert.Equal(FramePart.None, painter.PartAt(0, 0));
        Assert.Equal(FramePart.None, painter.PartAt(1, 0));
        Assert.Equal(FramePart.None, painter.PartAt(0, 1));
        Assert.Equal(FramePart.TopLeft, painter.PartAt(3, 0));
        Assert.Equal(FramePart.TopLeft, painter.PartAt(1, 1));
        Assert.Equal(FramePart.None, painter.PartAt(207, 0));
        Assert.False(painter.Opaque);

        painter.Paint(state, default, 2.0);
        Assert.Equal(FramePart.None, painter.PartAt(0.5, 0.5));
        Assert.Equal(FramePart.TopLeft, painter.PartAt(3, 0.5));
    }

    [Fact]
    public void A_maximized_plain_frame_is_opaque_and_does_not_resize()
    {
        using var rig = new MsStylesRig();
        var painter = new MsStylesFramePainter(rig.Style);
        var state = MsStylesRig.StateFor("maximized");
        painter.Layout(state, 200, 60);
        painter.Paint(state, default, 1.0);

        Assert.True(painter.Opaque);
        Assert.Equal(FramePart.Title, painter.PartAt(100, 1));
        Assert.Equal(FramePart.None, painter.PartAt(100, 40));
    }

    [Fact]
    public void Glassy_glass_lays_out_inside_its_shadow()
    {
        using var rig = new MsStylesRig("Glassy", MsStylesLook.Glass);
        var painter = new MsStylesFramePainter(rig.Style);
        var state = MsStylesRig.StateFor("active");
        Assert.Equal(new FrameInsets(28, 8, 8, 8), painter.Measure(state));
        Assert.Equal(new FrameInsets(14, 20, 20, 14), painter.Outsets(state));
        Assert.True(painter.Layout(state, 200, 60));
        painter.Paint(state, default, 1.0);
        var geometry = painter.Geometry;

        Assert.Equal(216 + 34, geometry.Width);
        Assert.Equal(96 + 34, geometry.Height);
        Assert.Equal(new RectangleF(14, 14, 216, 96), geometry.WindowRect);
        Assert.Equal(new RectangleF(22, 42, 200, 60), geometry.ClientRect);
        Assert.Equal(
            [new RectangleF(180, 15, 44, 18), new RectangleF(155, 15, 25, 18), new RectangleF(129, 15, 26, 18)],
            geometry.Buttons.ToArray().Select(b => b.Bounds));
        Assert.Equal(FramePart.None, painter.PartAt(5, 50));
        Assert.Equal(FramePart.None, painter.PartAt(240, 50));
        Assert.Equal(FramePart.Left, painter.PartAt(15, 70));
        Assert.Equal(FramePart.Close, painter.PartAt(200, 20));

        painter.Layout(MsStylesRig.StateFor("utility"), 200, 60);
        Assert.Equal(new RectangleF(207, 19, 17, 17), painter.Geometry.Buttons[0].Bounds);

        Assert.Equal(default, painter.Outsets(MsStylesRig.StateFor("maximized")));
        painter.Layout(MsStylesRig.StateFor("maximized"), 200, 60);
        Assert.Equal(new RectangleF(154, -1, 44, 18), painter.Geometry.Buttons[0].Bounds);
    }

    [Fact]
    public void Glassy_glass_backdrop_is_the_ring_without_the_shadow()
    {
        using var rig = new MsStylesRig("Glassy", MsStylesLook.Glass);
        var renderer = rig.Renderer();
        var painter = renderer.Painter;
        var state = MsStylesRig.StateFor("active");
        painter.Layout(state, 200, 60);
        using var region = new Pixman.PixmanRegion32();
        Assert.True(renderer.BackdropRegion(state, 1.0, region));

        Assert.True(region.Contains(20, 30));
        Assert.True(region.Contains(15, 70));
        Assert.True(region.Contains(225, 100));
        Assert.False(region.Contains(50, 60));
        Assert.False(region.Contains(5, 50));
        Assert.False(region.Contains(240, 50));
        Assert.False(region.Contains(14, 14));
        Assert.True(region.Contains(19, 14));

        painter.Layout(MsStylesRig.StateFor("maximized"), 200, 60);
        Assert.False(renderer.BackdropRegion(MsStylesRig.StateFor("maximized"), 1.0, region));
        Assert.True(region.IsEmpty);
    }
}
