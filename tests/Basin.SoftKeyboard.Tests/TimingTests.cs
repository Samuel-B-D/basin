using Xunit;

namespace Basin.SoftKeyboard.Tests;

public sealed class TimingTests
{
    [Fact]
    public void A_held_backspace_repeats_at_350_then_every_120_and_once_more_on_release()
    {
        var engine = Keys.Engine();
        var typed = Keys.Record(engine);
        Keys.Down(engine, "backspace", 0);
        Assert.Equal(350, engine.NextDeadline);
        engine.Tick(349);
        Assert.Empty(typed);
        engine.Tick(350);
        Assert.Single(typed);
        Assert.True(typed[0].Repeat);
        Assert.Equal(470, engine.NextDeadline);
        engine.Tick(470);
        Assert.Equal(2, typed.Count);
        engine.TouchUp(1, 500);
        Assert.Equal(3, typed.Count);
        Assert.False(typed[2].Repeat);
        Assert.Equal(long.MaxValue, engine.NextDeadline);
    }

    [Fact]
    public void Backspace_turns_to_word_delete_after_1850_ms()
    {
        var engine = Keys.Engine();
        var typed = Keys.Record(engine);
        Keys.Down(engine, "backspace", 0);
        for (var now = 0L; now <= 2400; now += 5)
        {
            engine.Tick(now);
        }

        var firstWord = typed.FindIndex(static t => t.WordDelete);
        Assert.True(firstWord > 0);
        Assert.All(typed.Take(firstWord), static t => Assert.False(t.WordDelete));
        Assert.Equal(1910, 350 + (120 * firstWord));
        Assert.Equal(1910 + 275 + 275, engine.NextDeadline);
    }

    [Fact]
    public void Shift_held_makes_backspace_delete_words_at_once()
    {
        var engine = Keys.Engine();
        var typed = Keys.Record(engine);
        Keys.Down(engine, "shift", 0, id: 2);
        Keys.Down(engine, "backspace", 10, id: 1);
        engine.Tick(360);
        Assert.True(typed.Single(static t => t.Repeat).WordDelete);
        Assert.Equal(360 + 275, engine.NextDeadline);
    }

    [Fact]
    public void Space_repeats_but_a_letter_does_not()
    {
        var engine = Keys.Engine();
        var typed = Keys.Record(engine);
        Keys.Down(engine, "f", 0);
        Assert.Equal(long.MaxValue, engine.NextDeadline);
        engine.TouchUp(1, 600);
        Keys.Down(engine, " ", 1000);
        engine.Tick(1350);
        Assert.Equal("f ", Keys.Text(typed));
    }

    [Fact]
    public void A_long_press_opens_at_350_ms_and_at_750_for_hide()
    {
        var engine = Keys.Engine();
        var opened = new List<int>();
        engine.LongPressOpened += (cell, _) => opened.Add(cell);
        Keys.Down(engine, "e", 0);
        engine.Tick(349);
        Assert.Empty(opened);
        engine.Tick(350);
        Assert.Equal(Keys.Find(engine, "e"), Assert.Single(opened));
        engine.TouchUp(1, 400);
        Assert.True(engine.IsLongPressOpen);
        engine.CloseLongPress();

        Keys.Down(engine, "hide", 1000, id: 2);
        engine.Tick(1350);
        Assert.Single(opened);
        engine.Tick(1750);
        Assert.Equal(2, opened.Count);
        Assert.Equal(KeyCap.ExtraSmall, engine.LongPressEntries![0]);
    }

    [Fact]
    public void A_slow_long_press_waits_750_ms_for_every_key()
    {
        var engine = Keys.Engine();
        engine.SlowLongPress = true;
        Keys.Down(engine, "e", 0);
        Assert.Equal(750, engine.NextDeadline);
    }

    [Fact]
    public void A_long_press_is_never_sent_as_the_key()
    {
        var engine = Keys.Engine();
        var typed = Keys.Record(engine);
        Keys.Down(engine, "e", 0);
        engine.Tick(350);
        engine.TouchUp(1, 400);
        Assert.Empty(typed);
    }

    [Fact]
    public void Sliding_onto_an_entry_and_lifting_chooses_it()
    {
        var engine = Keys.Engine();
        var typed = Keys.Record(engine);
        var chosen = new List<KeyCap>();
        engine.LongPressChosen += chosen.Add;
        Keys.Down(engine, "e", 0);
        engine.Tick(350);
        var cell = engine.LongPressGeometry.CellRect(2);
        engine.TouchMotion(1, cell.CenterX, cell.CenterY, 380);
        Assert.Equal(2, engine.LongPressHighlightIndex);
        engine.TouchUp(1, 400);
        Assert.Equal("é", Keys.Text(typed));
        Assert.Equal("é", Assert.Single(chosen).Text);
        Assert.False(engine.IsLongPressOpen);
    }

    [Fact]
    public void A_list_left_open_takes_a_tap_or_switches_or_closes()
    {
        var engine = Keys.Engine();
        var typed = Keys.Record(engine);
        Keys.Down(engine, "e", 0);
        engine.Tick(350);
        engine.TouchUp(1, 400);
        Assert.True(engine.Accepts(500, -200, 450));

        Keys.Tap(engine, "u", 500, id: 2);
        Assert.True(engine.IsLongPressOpen);
        Assert.Equal(Keys.Find(engine, "u"), engine.LongPressCell);

        Keys.Tap(engine, "f", 600, id: 3);
        Assert.False(engine.IsLongPressOpen);
        Assert.Empty(typed);

        Keys.Down(engine, "e", 1000, id: 4);
        engine.Tick(1350);
        engine.TouchUp(4, 1400);
        var cell = engine.LongPressGeometry.CellRect(1);
        engine.TouchDown(5, cell.CenterX, cell.CenterY, 1500);
        engine.Frame(1500);
        engine.TouchUp(5, 1550);
        Assert.Equal("è", Keys.Text(typed));
    }

    [Fact]
    public void Choosing_a_size_on_hide_raises_size_chosen()
    {
        var engine = Keys.Engine();
        var sizes = new List<KeyboardSize>();
        engine.SizeChosen += sizes.Add;
        Keys.Down(engine, "hide", 0);
        engine.Tick(750);
        engine.TouchUp(1, 800);
        engine.ChooseLongPress(3, 900);
        Assert.Equal(KeyboardSize.Large, Assert.Single(sizes));
    }

    [Fact]
    public void The_grace_band_accepts_40_px_above_for_500_ms_after_a_touch_begins()
    {
        var engine = Keys.Engine();
        Assert.True(engine.Accepts(100, -4, 0));
        Assert.False(engine.Accepts(100, -30, 0));
        Keys.Tap(engine, "a", 1000);
        Assert.True(engine.Accepts(100, -30, 1200));
        Assert.True(engine.Accepts(100, -44, 1499));
        Assert.False(engine.Accepts(100, -46, 1200));
        Assert.False(engine.Accepts(100, -30, 1500));
    }

    [Fact]
    public void A_canceled_touch_sends_nothing()
    {
        var engine = Keys.Engine();
        var typed = Keys.Record(engine);
        Keys.Down(engine, " ", 0);
        engine.Cancel(1);
        engine.TouchUp(1, 100);
        engine.Tick(400);
        Assert.Empty(typed);
    }
}
