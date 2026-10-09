using Xunit;

namespace Basin.SoftKeyboard.Tests;

public sealed class StateMachineTests
{
    [Fact]
    public void A_second_shift_tap_within_500_ms_locks()
    {
        var engine = Keys.Engine();
        Keys.Tap(engine, "shift", 0);
        Assert.Equal(ShiftMode.Once, engine.State.Shift);
        Keys.Tap(engine, "shift", 499);
        Assert.Equal(ShiftMode.Lock, engine.State.Shift);
    }

    [Fact]
    public void A_second_shift_tap_at_501_ms_toggles_off()
    {
        var engine = Keys.Engine();
        Keys.Tap(engine, "shift", 0);
        Keys.Tap(engine, "shift", 501);
        Assert.Equal(ShiftMode.Off, engine.State.Shift);
    }

    [Fact]
    public void A_tap_that_unlocks_eats_a_quick_tap_after_it()
    {
        var engine = Keys.Engine();
        Keys.Tap(engine, "shift", 0);
        Keys.Tap(engine, "shift", 100);
        Assert.Equal(ShiftMode.Lock, engine.State.Shift);
        Keys.Tap(engine, "shift", 2000);
        Assert.Equal(ShiftMode.Off, engine.State.Shift);
        Keys.Tap(engine, "shift", 2300);
        Assert.Equal(ShiftMode.Off, engine.State.Shift);
        Keys.Tap(engine, "shift", 3500);
        Assert.Equal(ShiftMode.Once, engine.State.Shift);
    }

    [Fact]
    public void Once_types_one_capital_and_clears()
    {
        var engine = Keys.Engine();
        var typed = Keys.Record(engine);
        Keys.Tap(engine, "shift", 0);
        Keys.Tap(engine, "h", 1000);
        Keys.Tap(engine, "i", 1100);
        Assert.Equal("Hi", Keys.Text(typed));
        Assert.Equal(ShiftMode.Off, engine.State.Shift);
    }

    [Fact]
    public void Lock_keeps_capitals_and_leaves_the_number_row_as_digits()
    {
        var engine = Keys.Engine();
        var typed = Keys.Record(engine);
        Keys.Tap(engine, "shift", 0);
        Keys.Tap(engine, "shift", 100);
        Keys.Tap(engine, "a", 1000);
        Keys.Tap(engine, "b", 1100);
        Keys.Tap(engine, "1", 1200);
        Assert.Equal("AB1", Keys.Text(typed));
        Assert.Equal(ShiftMode.Lock, engine.State.Shift);
    }

    [Fact]
    public void Holding_shift_while_typing_is_momentary_and_its_release_does_nothing()
    {
        var engine = Keys.Engine();
        var typed = Keys.Record(engine);
        Keys.Down(engine, "shift", 0, id: 1);
        Assert.True(engine.State.ShiftActive);
        Keys.Tap(engine, "a", 50, id: 2);
        Keys.Tap(engine, "1", 80, id: 3);
        engine.TouchUp(1, 120);
        Assert.Equal("A!", Keys.Text(typed));
        Assert.Equal(ShiftMode.Off, engine.State.Shift);
        Assert.False(engine.State.ShiftHeld);
    }

    [Fact]
    public void Holding_symbol_shows_the_symbol_page_while_held()
    {
        var engine = Keys.Engine();
        var typed = Keys.Record(engine);
        Keys.Down(engine, "symbol", 0, id: 1);
        Assert.True(engine.State.SymbolActive);
        Keys.Tap(engine, "q", 50, id: 2);
        engine.TouchUp(1, 100);
        Keys.Tap(engine, "q", 200);
        Assert.Equal("`q", Keys.Text(typed));
        Assert.False(engine.State.SymbolLock);
    }

    [Fact]
    public void A_symbol_tap_locks_turns_shift_off_and_space_clears_it()
    {
        var engine = Keys.Engine();
        var typed = Keys.Record(engine);
        Keys.Tap(engine, "shift", 0);
        Keys.Tap(engine, "symbol", 1000);
        Assert.True(engine.State.SymbolLock);
        Assert.Equal(ShiftMode.Off, engine.State.Shift);
        Keys.Tap(engine, "q", 1100);
        Keys.Tap(engine, "w", 1200);
        Keys.Tap(engine, " ", 1300);
        Keys.Tap(engine, "q", 1400);
        Assert.Equal("`~ q", Keys.Text(typed));
        Assert.False(engine.State.SymbolLock);
    }

    [Fact]
    public void Return_clears_symbol_lock_and_a_second_symbol_tap_unlocks()
    {
        var engine = Keys.Engine();
        Keys.Tap(engine, "symbol", 0);
        Keys.Tap(engine, "return", 100);
        Assert.False(engine.State.SymbolLock);
        Keys.Tap(engine, "symbol", 200);
        Keys.Tap(engine, "symbol", 300);
        Assert.False(engine.State.SymbolLock);
    }

    [Fact]
    public void Shift_shows_a_non_letter_s_alternate_and_symbol_a_letter_s()
    {
        var engine = Keys.Engine();
        var q = engine.Grid[Keys.Find(engine, "q")];
        var one = engine.Grid[Keys.Find(engine, "1")];
        engine.State.SetShiftHeld(true);
        Assert.Equal("q", engine.State.Resolve(q)!.Text);
        Assert.Equal("!", engine.State.Resolve(one)!.Text);
        engine.State.SetShiftHeld(false);
        engine.State.SetSymbolHeld(true);
        Assert.Equal("`", engine.State.Resolve(q)!.Text);
        Assert.Equal("1", engine.State.Resolve(one)!.Text);
    }

    [Fact]
    public void Azerty_number_fields_give_digits_without_shift()
    {
        var engine = Keys.Engine("fr-azerty");
        var two = engine.Grid[Keys.Find(engine, "é")];
        Assert.Equal("é", engine.State.Resolve(two)!.Text);
        engine.State.NumLockField = true;
        Assert.Equal("2", engine.State.Resolve(two)!.Text);
        engine.State.SetShiftHeld(true);
        Assert.Equal("é", engine.State.Resolve(two)!.Text);
    }

    [Fact]
    public void A_press_sends_any_held_unsent_key_which_is_rollover()
    {
        var engine = Keys.Engine();
        var typed = Keys.Record(engine);
        Keys.Down(engine, "a", 0, id: 1);
        Assert.Empty(typed);
        Keys.Down(engine, "s", 30, id: 2);
        Assert.Equal("a", Keys.Text(typed));
        engine.TouchUp(1, 60);
        Assert.Equal("a", Keys.Text(typed));
        engine.TouchUp(2, 90);
        Assert.Equal("as", Keys.Text(typed));
    }

    [Fact]
    public void Releases_in_a_frame_are_handled_before_its_presses()
    {
        var engine = Keys.Engine();
        var typed = Keys.Record(engine);
        Keys.Down(engine, "a", 0, id: 1);
        var (x, y) = Keys.Center(engine, "s");
        engine.TouchDown(2, x, y, 30);
        engine.TouchUp(1, 30);
        engine.Frame(30);
        engine.TouchUp(2, 60);
        Assert.Equal("as", Keys.Text(typed));
    }

    [Fact]
    public void Sliding_moves_the_press_to_the_key_under_the_finger()
    {
        var engine = Keys.Engine();
        var typed = Keys.Record(engine);
        Keys.Down(engine, "a", 0);
        Assert.True(engine.IsPressed(Keys.Find(engine, "a")));
        var (x, y) = Keys.Center(engine, "s");
        engine.TouchMotion(1, x, y, 20);
        Assert.False(engine.IsPressed(Keys.Find(engine, "a")));
        Assert.True(engine.IsPressed(Keys.Find(engine, "s")));
        engine.TouchUp(1, 40);
        Assert.Equal("s", Keys.Text(typed));
    }

    [Fact]
    public void Auto_cap_types_a_capital_and_clears_after_one_key()
    {
        var engine = Keys.Engine();
        var typed = Keys.Record(engine);
        engine.State.SetAutoCap(true);
        Keys.Tap(engine, "h", 0);
        Keys.Tap(engine, "i", 100);
        Assert.Equal("Hi", Keys.Text(typed));
    }

    [Fact]
    public void A_shift_tap_clears_auto_cap()
    {
        var engine = Keys.Engine();
        engine.State.SetAutoCap(true);
        Keys.Tap(engine, "shift", 0);
        Assert.False(engine.State.AutoCap);
        Assert.Equal(ShiftMode.Once, engine.State.Shift);
    }

    [Fact]
    public void Hide_and_the_language_key_raise_their_events()
    {
        var engine = new SoftKeyboardEngine(Keys.Layout().Grid(BottomRow.Default, [KeyCap.LanguageChoice(0, "En"), KeyCap.LanguageChoice(1, "Fr")]));
        engine.Layout(1024, 335, 5);
        var hides = 0;
        var nexts = 0;
        engine.HideRequested += () => hides++;
        engine.LanguageNext += () => nexts++;
        Keys.Tap(engine, "hide", 0);
        Keys.Tap(engine, "language", 100);
        Assert.Equal(1, hides);
        Assert.Equal(1, nexts);
    }
}
