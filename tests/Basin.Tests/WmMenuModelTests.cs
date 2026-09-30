using Basin.WindowManager;
using Xunit;
using WmPoint = Basin.WindowManager.Point;
using WmRect = Basin.WindowManager.Rect;
using WmSize = Basin.WindowManager.Size;

namespace Basin.Tests;

public sealed class WmMenuModelTests
{
    private static WmMenuModel Menu()
    {
        var model = new WmMenuModel(4);
        model.SetRow(0, 20);
        model.SetRow(1, 7, selectable: false);
        model.SetRow(2, 20);
        model.SetRow(3, 20);
        model.Content = new WmRect(1, 5, 100, model.RowsHeight());
        return model;
    }

    [Fact]
    public void A_point_finds_its_row_and_a_separator_is_not_selectable()
    {
        var model = Menu();
        Assert.Null(model.RowAt(10, 4));
        Assert.Equal(0, model.RowAt(10, 5));
        Assert.Equal(1, model.RowAt(10, 26));
        Assert.Null(model.SelectableAt(10, 26));
        Assert.Equal(2, model.RowAt(10, 32));
        Assert.Null(model.RowAt(0, 32));
        Assert.Null(model.RowAt(10, 72));
        Assert.Equal(new WmRect(1, 32, 100, 20), model.RowRect(2));
    }

    [Fact]
    public void Hover_reports_only_a_change()
    {
        var model = Menu();
        Assert.True(model.Hover(10, 10));
        Assert.False(model.Hover(10, 12));
        Assert.True(model.Hover(10, 26));
        Assert.Equal(-1, model.Hovered);
    }

    [Fact]
    public void A_step_wraps_and_skips_what_cannot_be_selected()
    {
        var model = Menu();
        Assert.True(model.SelectFirst());
        Assert.Equal(0, model.Hovered);
        model.Step(1);
        Assert.Equal(2, model.Hovered);
        model.Step(1);
        model.Step(1);
        Assert.Equal(0, model.Hovered);
        model.Step(-1);
        Assert.Equal(3, model.Hovered);
    }

    [Fact]
    public void A_render_is_needed_after_a_hover_or_scale_change_only()
    {
        var model = Menu();
        Assert.True(model.NeedsRender(1));
        model.Rendered(1);
        Assert.False(model.NeedsRender(1));
        Assert.True(model.NeedsRender(2));
        model.Hover(10, 10);
        Assert.True(model.NeedsRender(1));
    }

    [Fact]
    public void An_origin_is_clamped_into_the_area() =>
        Assert.Equal(new WmPoint(80, 0), WmMenuModel.ClampInto(new WmPoint(150, -10), new WmSize(20, 20), new WmSize(100, 100)));
}
