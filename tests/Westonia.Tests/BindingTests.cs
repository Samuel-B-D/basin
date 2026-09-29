using Basin;
using Xkb;
using Xunit;

namespace Westonia.Tests;

public sealed class BindingTests
{
    [Theory]
    [InlineData("super", ShellModifiers.Super)]
    [InlineData("Super", ShellModifiers.Super)]
    [InlineData("ctrl", ShellModifiers.Ctrl)]
    [InlineData("alt", ShellModifiers.Alt)]
    [InlineData("none", ShellModifiers.None)]
    [InlineData("nonsense", ShellModifiers.Super)]
    public void The_binding_modifier_resolves_from_weston_ini(string value, ShellModifiers expected) =>
        Assert.Equal(expected, BindingModifiers.Parse(value));

    private sealed class Keys : IDisposable
    {
        private readonly XkbContext _context = XkbContext.Create();
        private readonly XkbKeymap _keymap;

        public Keys()
        {
            _keymap = _context.CreateKeymap(new XkbRuleNames { Layout = "us" });
            State = _keymap.CreateState();
            Modifiers = new ShellModifierState(() => State);
        }

        public XkbState State { get; }

        public ShellModifierState Modifiers { get; }

        public void Press(uint key) => State.UpdateKey(key + 8, XkbKeyDirection.Down);

        public void Release(uint key) => State.UpdateKey(key + 8, XkbKeyDirection.Up);

        public void Dispose()
        {
            State.Dispose();
            _keymap.Dispose();
            _context.Dispose();
        }
    }

    [Fact]
    public void Modifiers_come_from_the_xkb_state_rather_than_from_tracked_key_codes()
    {
        using var keys = new Keys();
        var state = keys.Modifiers;

        keys.Press(InputCodes.KeyLeftMeta);
        Assert.True(state.Holds(ShellModifiers.Super));

        keys.Press(InputCodes.KeyLeftShift);
        Assert.True(state.Holds(ShellModifiers.Super | ShellModifiers.Shift));
        Assert.False(state.Exactly(ShellModifiers.Super));

        keys.Release(InputCodes.KeyLeftShift);
        Assert.True(state.Exactly(ShellModifiers.Super));

        keys.Release(InputCodes.KeyLeftMeta);
        Assert.Equal(ShellModifiers.None, state.Current);
    }

    [Fact]
    public void A_mask_the_parent_sends_replaces_whatever_was_held()
    {
        using var keys = new Keys();
        keys.Press(InputCodes.KeyLeftMeta);
        Assert.True(keys.Modifiers.Holds(ShellModifiers.Super));

        keys.State.UpdateMask(0, 0, 0, 0, 0, 0);

        Assert.Equal(ShellModifiers.None, keys.Modifiers.Current);
    }

    [Fact]
    public void Both_sides_of_a_modifier_count_as_the_same_modifier()
    {
        using var keys = new Keys();
        var state = keys.Modifiers;

        keys.Press(InputCodes.KeyLeftCtrl);
        keys.Press(InputCodes.KeyRightCtrl);
        keys.Release(InputCodes.KeyLeftCtrl);

        Assert.True(state.Holds(ShellModifiers.Ctrl));

        keys.Release(InputCodes.KeyRightCtrl);
        Assert.False(state.Holds(ShellModifiers.Ctrl));
    }

    [Fact]
    public void An_ordinary_key_is_not_a_modifier()
    {
        using var keys = new Keys();

        Assert.False(keys.Modifiers.IsModifierKey(InputCodes.KeyTab));
        Assert.True(keys.Modifiers.IsModifierKey(InputCodes.KeyLeftMeta));
        Assert.True(keys.Modifiers.IsModifierKey(InputCodes.KeyRightCtrl));
        keys.Press(InputCodes.KeyTab);
        Assert.Equal(ShellModifiers.None, keys.Modifiers.Current);
    }

    [Fact]
    public void The_zap_chord_is_ctrl_alt_backspace_and_nothing_else()
    {
        using var keys = new Keys();
        keys.Press(InputCodes.KeyLeftCtrl);
        keys.Press(InputCodes.KeyLeftAlt);

        Assert.True(keys.Modifiers.Exactly(ShellModifiers.Ctrl | ShellModifiers.Alt));

        keys.Press(InputCodes.KeyLeftShift);
        Assert.False(keys.Modifiers.Exactly(ShellModifiers.Ctrl | ShellModifiers.Alt));
    }

    [Fact]
    public void Carrying_a_window_avoids_the_tiled_orientation_chord()
    {
        using var keys = new Keys();
        var state = keys.Modifiers;
        keys.Press(InputCodes.KeyLeftMeta);
        keys.Press(InputCodes.KeyLeftShift);

        Assert.True(state.Holds(ShellModifiers.Super | ShellModifiers.Shift));
        Assert.False(state.Holds(ShellModifiers.Ctrl));

        keys.Release(InputCodes.KeyLeftShift);
        keys.Press(InputCodes.KeyLeftCtrl);

        Assert.True(state.Holds(ShellModifiers.Super | ShellModifiers.Ctrl));
        Assert.False(state.Holds(ShellModifiers.Shift));
    }

    [Fact]
    public void The_workspace_jump_keys_are_six_consecutive_function_keys()
    {
        Assert.Equal(59u, InputCodes.KeyF1);
        Assert.Equal(64u, InputCodes.KeyF1 + 5);
    }
}
