using Avalonia;
using Avalonia.Controls;

namespace System7Wm;

internal sealed class FinderIconPanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        var height = 0.0;
        foreach (var child in Children)
        {
            child.Measure(Size.Infinity);
            var left = Canvas.GetLeft(child);
            var top = Canvas.GetTop(child);
            width = Math.Max(width, (double.IsNaN(left) ? 0 : left) + child.DesiredSize.Width);
            height = Math.Max(height, (double.IsNaN(top) ? 0 : top) + child.DesiredSize.Height);
        }

        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            var left = Canvas.GetLeft(child);
            var top = Canvas.GetTop(child);
            child.Arrange(new Rect(new Point(double.IsNaN(left) ? 0 : left, double.IsNaN(top) ? 0 : top), child.DesiredSize));
        }

        return finalSize;
    }
}
