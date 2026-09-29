using System.Globalization;
using Basin.Capabilities;

namespace Westonia;

public static class WestonInputRules
{
    public static IReadOnlyList<InputRule> From(WestonIni ini, Action<string>? warn = null)
    {
        ArgumentNullException.ThrowIfNull(ini);
        var rules = new List<InputRule>();
        var libinput = new List<(InputSetting, InputSettingValue)>();
        foreach (var (key, raw) in ini.Libinput)
        {
            if (SettingFor(key) is not { } setting || InputSettingNames.IsKeyboard(setting))
            {
                warn?.Invoke($"weston.ini: [libinput] {key} is not honored");
                continue;
            }

            if (!InputSettingNames.TryParse(setting, ValueFor(setting, raw), out var value))
            {
                warn?.Invoke($"weston.ini: [libinput] {key}={raw} is not a value it takes");
                continue;
            }

            libinput.Add((setting, value));
        }

        if (libinput.Count > 0)
        {
            rules.Add(new InputRule { Match = default, Settings = libinput });
        }

        var keyboard = ini.Keyboard;
        var keys = new List<(InputSetting, InputSettingValue)>();
        AddText(keys, InputSetting.KeymapRules, keyboard.Rules);
        AddText(keys, InputSetting.KeymapModel, keyboard.Model);
        AddText(keys, InputSetting.KeymapLayout, keyboard.Layout);
        AddText(keys, InputSetting.KeymapVariant, keyboard.Variant);
        AddText(keys, InputSetting.KeymapOptions, keyboard.Options);
        keys.Add((InputSetting.RepeatRate, InputSettingValue.Of((uint)Math.Max(0, keyboard.RepeatRate))));
        keys.Add((InputSetting.RepeatDelay, InputSettingValue.Of((uint)Math.Max(0, keyboard.RepeatDelay))));
        rules.Add(new InputRule { Match = new InputMatch(AnyKeyboard: true), Settings = keys });
        return rules;
    }

    private static void AddText(List<(InputSetting, InputSettingValue)> keys, InputSetting setting, string? text)
    {
        if (text is { Length: > 0 })
        {
            keys.Add((setting, InputSettingValue.Text(text)));
        }
    }

    private static InputSetting? SettingFor(string key) => key switch
    {
        "enable-tap" or "enable_tap" => InputSetting.Tap,
        "tap-and-drag" => InputSetting.Drag,
        "tap-and-drag-lock" => InputSetting.DragLock,
        "disable-while-typing" => InputSetting.DisableWhileTyping,
        "middle-button-emulation" => InputSetting.MiddleEmulation,
        _ => InputSettingNames.TryParseKey(key, out var setting) ? setting : null,
    };

    private static string ValueFor(InputSetting setting, string value) => setting switch
    {
        InputSetting.ScrollMethod when value == "button" => "on_button_down",
        InputSetting.ScrollButton => ButtonCode(value),
        _ => value,
    };

    private static string ButtonCode(string value) => value switch
    {
        "BTN_LEFT" => "272",
        "BTN_RIGHT" => "273",
        "BTN_MIDDLE" => "274",
        "BTN_SIDE" => "275",
        "BTN_EXTRA" => "276",
        "BTN_FORWARD" => "277",
        "BTN_BACK" => "278",
        "BTN_TASK" => "279",
        _ => value.ToString(CultureInfo.InvariantCulture),
    };
}
