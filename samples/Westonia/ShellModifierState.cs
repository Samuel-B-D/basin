using Xkb;

namespace Westonia;

public sealed class ShellModifierState(Func<XkbState?> state)
{
    public ShellModifiers Current
    {
        get
        {
            var held = Basin.Config.HeldModifiers.From(state());
            var shell = ShellModifiers.None;
            if ((held & Basin.Config.Modifiers.Ctrl) != 0)
            {
                shell |= ShellModifiers.Ctrl;
            }

            if ((held & Basin.Config.Modifiers.Alt) != 0)
            {
                shell |= ShellModifiers.Alt;
            }

            if ((held & Basin.Config.Modifiers.Shift) != 0)
            {
                shell |= ShellModifiers.Shift;
            }

            if ((held & Basin.Config.Modifiers.Super) != 0)
            {
                shell |= ShellModifiers.Super;
            }

            return shell;
        }
    }

    public bool IsModifierKey(uint key) => state()?.GetKeyOneSym(key + 8).Value is (>= 0xffe1 and <= 0xffee) or (>= 0xfe01 and <= 0xfe0f) or 0xff7e;

    public bool Holds(ShellModifiers modifiers) => (Current & modifiers) == modifiers;

    public bool Exactly(ShellModifiers modifiers) => Current == modifiers;
}
