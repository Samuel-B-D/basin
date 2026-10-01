using System.Drawing;
using Basin.Capabilities;
using Basin.Frames.MsStyles;
using Xunit;

namespace Basin.Tests;

public sealed class MsStylesStreakTests
{
    private const int CellWidth = 118;
    private const int CellHeight = 88;

    private static (RectangleF Source, RectangleF Destination, Rectangle Cell) FirstStreak(MsStylesFramePainter painter, in FrameState state)
    {
        painter.Layout(state, 200, 40);
        painter.Paint(state, default, 1.0);
        foreach (ref readonly var op in painter.List.Ops)
        {
            if (op.Kind == MsStylesDrawOpKind.Image && op.Frame.Width == CellWidth && op.Frame.Height == CellHeight && op.Destination.Height >= 10)
            {
                return (op.Source, op.Destination, op.Frame);
            }
        }

        throw new InvalidOperationException("no streak was drawn");
    }

    private static FrameState At(int x, int y) => MsStylesRig.StateFor("active") with { Placement = new FramePlacement(x, y, 8 * CellWidth, 3 * CellHeight) };

    [Fact]
    public void The_streak_is_stretched_over_the_output()
    {
        using var rig = new MsStylesRig("Glassy", MsStylesLook.Glass);
        var (source, destination, _) = FirstStreak(new MsStylesFramePainter(rig.Style), At(200, 100));

        Assert.Equal(0.125f, source.Width / destination.Width, 3);
        Assert.Equal(1f / 3, source.Height / destination.Height, 3);
    }

    [Fact]
    public void A_window_drags_the_streak_an_eighth_of_its_horizontal_travel()
    {
        using var rig = new MsStylesRig("Glassy", MsStylesLook.Glass);
        var painter = new MsStylesFramePainter(rig.Style);
        var (before, _, _) = FirstStreak(painter, At(200, 100));
        var (right, _, _) = FirstStreak(painter, At(280, 100));
        var (down, _, _) = FirstStreak(painter, At(200, 130));

        Assert.Equal(before.Left + ((80 - 10) * 0.125f), right.Left, 3);
        Assert.Equal(before.Top, right.Top, 3);
        Assert.Equal(before.Left, down.Left, 3);
        Assert.Equal(before.Top + (30f / 3), down.Top, 3);
    }

    [Fact]
    public void A_window_centered_on_the_output_reads_the_middle_of_the_texture()
    {
        using var rig = new MsStylesRig("Glassy", MsStylesLook.Glass);
        var painter = new MsStylesFramePainter(rig.Style);
        var (source, destination, cell) = FirstStreak(painter, At((4 * CellWidth) - 100, 100));
        var window = painter.Geometry.WindowRect;
        var centerLocal = window.Left + (window.Width / 2);
        var underCenter = source.Left - cell.X + ((centerLocal - destination.Left) * 0.125f);

        Assert.Equal((CellWidth / 2f) + (0.5f * 0.125f) + 0.5f, underCenter, 3);
    }

    [Fact]
    public void Only_glass_follows_the_placement()
    {
        using var glass = new MsStylesRig("Glassy", MsStylesLook.Glass);
        using var basic = new MsStylesRig("Glassy", MsStylesLook.Basic);

        Assert.True(glass.Renderer().FollowsPlacement);
        Assert.False(basic.Renderer().FollowsPlacement);
    }
}
