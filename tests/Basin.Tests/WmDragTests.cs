using Basin.WindowManager;
using Xunit;
using Shape = Basin.WindowManager.Protocol.WpCursorShapeDeviceV1.Shape;

namespace Basin.Tests;

public sealed class WmDragTests
{
    [Theory]
    [InlineData(10, 10, Edges.Top | Edges.Left)]
    [InlineData(90, 10, Edges.Top | Edges.Right)]
    [InlineData(10, 90, Edges.Bottom | Edges.Left)]
    [InlineData(90, 90, Edges.Bottom | Edges.Right)]
    public void The_quadrant_under_the_pointer_names_the_edges(int x, int y, Edges expected) =>
        Assert.Equal(expected, WmDrag.Quadrant(new Basin.WindowManager.Rect(0, 0, 100, 100), new Basin.WindowManager.Point(x, y)));

    [Fact]
    public void A_hint_with_equal_nonzero_bounds_is_fixed()
    {
        Assert.True(new DimensionsHint(new Basin.WindowManager.Size(200, 100), new Basin.WindowManager.Size(200, 100)).IsFixed);
        Assert.False(new DimensionsHint(new Basin.WindowManager.Size(200, 100), default).IsFixed);
        Assert.False(new DimensionsHint(default, default).IsFixed);
    }

    [Fact]
    public void Each_edge_set_has_its_cursor()
    {
        Assert.Equal(Shape.NwResize, EdgeCursors.ShapeFor(Edges.Top | Edges.Left));
        Assert.Equal(Shape.SeResize, EdgeCursors.ShapeFor(Edges.Bottom | Edges.Right));
        Assert.Equal(Shape.EResize, EdgeCursors.ShapeFor(Edges.Right));
        Assert.Equal(Shape.Default, EdgeCursors.ShapeFor(Edges.None));
    }

    [Fact]
    public void A_second_click_on_the_same_target_within_the_threshold_is_a_double_click()
    {
        var first = new object();
        var clicks = new WmDoubleClick<object>();
        Assert.False(clicks.Click(first, 1000));
        Assert.True(clicks.Click(first, 1000 + WmDoubleClick<object>.ThresholdMs));
        Assert.False(clicks.Click(first, 1500));
        Assert.False(clicks.Click(new object(), 1600));
        Assert.False(clicks.Click(first, 1700));
        clicks.Forget(first);
        Assert.False(clicks.Click(first, 1800));
    }
}
