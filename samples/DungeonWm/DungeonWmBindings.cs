using Basin.Config;
using Basin.Diagnostics;

namespace DungeonWm;

internal sealed class DungeonWmBindings
{
    public static readonly string[] Actions = ["leave-takeover", "next-room", "previous-room", "takeover", "close", "throw-back", "look-reset", "use", "release", "rotate", "square"];

    private readonly List<(uint Keysym, Modifiers Mask, string Action)> _bindings = [];

    public DungeonWmBindings(IReadOnlyDictionary<string, string> table, BasinLogger log)
    {
        foreach (var (chord, action) in table)
        {
            if (HotkeyParser.TryParseChord(chord, log, out var keysym, out var mask))
            {
                _bindings.Add((new Xkb.XkbKeysym(keysym).ToLower().Value, mask, action));
            }
        }

        Implicit('e', Modifiers.None, "use");
        Implicit('r', Modifiers.None, "rotate");
        Implicit('r', Modifiers.Shift, "square");
    }

    public int Count => _bindings.Count;

    private void Implicit(char key, Modifiers mask, string action)
    {
        var keysym = new Xkb.XkbKeysym(key).Value;
        if (!_bindings.Exists(binding => binding.Action == action) && Match(keysym, mask) is null)
        {
            _bindings.Add((keysym, mask, action));
        }
    }

    public static bool IsAction(string name) =>
        Array.IndexOf(Actions, name) >= 0 || (name.StartsWith("spawn ", StringComparison.Ordinal) && name.AsSpan(6).Trim().Length > 0);

    public string? Match(uint keysym, Modifiers held)
    {
        foreach (var (symbol, mask, action) in _bindings)
        {
            if (symbol == keysym && mask == held)
            {
                return action;
            }
        }

        return null;
    }

    public static Modifiers Held(Basin.Seat.SeatKeyboard keyboard)
    {
        if (keyboard.State is not { } state)
        {
            return Modifiers.None;
        }

        var held = Modifiers.None;
        if (state.IsModActive("Shift"))
        {
            held |= Modifiers.Shift;
        }

        if (state.IsModActive("Control"))
        {
            held |= Modifiers.Ctrl;
        }

        if (state.IsModActive("Mod1"))
        {
            held |= Modifiers.Mod1;
        }

        if (state.IsModActive("Mod4"))
        {
            held |= Modifiers.Mod4;
        }

        return held;
    }
}
