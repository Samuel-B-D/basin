using Xkb;

namespace Westonia;

public sealed class ShellModifierState(Func<XkbState?> state)
{
    public ShellModifiers Current
    {
        get
        {
            if (state() is not { } xkb)
            {
                return ShellModifiers.None;
            }

            var held = ShellModifiers.None;
            if (xkb.IsModActive("Control"))
            {
                held |= ShellModifiers.Ctrl;
            }

            if (xkb.IsModActive("Mod1"))
            {
                held |= ShellModifiers.Alt;
            }

            if (xkb.IsModActive("Shift"))
            {
                held |= ShellModifiers.Shift;
            }

            if (xkb.IsModActive("Mod4"))
            {
                held |= ShellModifiers.Super;
            }

            return held;
        }
    }

    public bool IsModifierKey(uint key) => state()?.GetKeyOneSym(key + 8).Value is (>= 0xffe1 and <= 0xffee) or (>= 0xfe01 and <= 0xfe0f) or 0xff7e;

    public bool Holds(ShellModifiers modifiers) => (Current & modifiers) == modifiers;

    public bool Exactly(ShellModifiers modifiers) => Current == modifiers;
}
