using Xunit;

namespace Basin.SoftKeyboard.Tests;

public sealed class LongPressFrameTests
{
    [Fact]
    public void Five_entries_or_fewer_take_one_line_of_90_px()
    {
        var key = new KeyRect(400, 125, 93, 70);
        var frame = LongPressFrame.For(key, 4, 1024);
        Assert.Equal(1, frame.Lines);
        Assert.Equal(4, frame.LineLength);
        Assert.Equal(90, frame.Frame.Height);
        Assert.Equal(21 + (4 * 80), frame.Frame.Width);
        Assert.Equal(key.CenterX - 51, frame.Frame.X);
        Assert.Equal(key.Y - 90 + 10, frame.Frame.Y);
    }

    [Fact]
    public void Six_entries_or_more_take_two_lines_of_ceil_half()
    {
        var frame = LongPressFrame.For(new KeyRect(400, 125, 93, 70), 7, 1024);
        Assert.Equal(2, frame.Lines);
        Assert.Equal(4, frame.LineLength);
        Assert.Equal(150, frame.Frame.Height);
        Assert.Equal(5, frame.EntryAt(frame.CellRect(5).CenterX, frame.CellRect(5).CenterY));
        Assert.Equal(LongPressFrame.Nothing, frame.EntryAt(frame.CellRect(3).CenterX, frame.CellRect(3).CenterY + 60));
        Assert.Equal(LongPressFrame.Outside, frame.EntryAt(frame.Frame.X - 5, frame.Frame.Y + 20));
    }

    [Fact]
    public void The_frame_is_clamped_inside_the_keyboard_but_the_pointer_stays_on_the_key()
    {
        var left = LongPressFrame.For(new KeyRect(0, 125, 93, 70), 5, 1024);
        Assert.Equal(0, left.Frame.X);
        Assert.Equal(46.5f, left.PointerX);
        var right = LongPressFrame.For(new KeyRect(931, 125, 93, 70), 5, 1024);
        Assert.Equal(1024, right.Frame.Right, 0.01f);
        Assert.Equal(977.5f, right.PointerX);
    }
}
