using System.Globalization;

namespace Basin.Capabilities;

public static class InputSettingNames
{
    private static readonly InputSetting[] AllSettings = Enum.GetValues<InputSetting>();

    private static readonly InputDeviceType[] AllTypes = Enum.GetValues<InputDeviceType>();

    private static readonly string[] FlagWords = ["true", "false"];

    private static readonly string[] ButtonMapWords = ["lrm", "lmr"];

    private static readonly string[] DragLockWords = ["disabled", "enabled", "enabled_sticky"];

    private static readonly string[] ThreeFingerDragWords = ["disabled", "enabled_3fg", "enabled_4fg"];

    private static readonly string[] ClickMethodWords = ["none", "button_areas", "clickfinger"];

    private static readonly string[] ScrollMethodWords = ["none", "two_finger", "edge", "on_button_down"];

    private static readonly string[] AccelProfileWords = ["none", "flat", "adaptive", "custom"];

    private static readonly string[] SendEventsWords = ["enabled", "disabled", "disabled_on_external_mouse"];

    public static ReadOnlySpan<InputSetting> Settings => AllSettings;

    public static ReadOnlySpan<InputDeviceType> Types => AllTypes;

    public static string Key(InputSetting setting) => setting switch
    {
        InputSetting.SendEvents => "send_events",
        InputSetting.Tap => "tap",
        InputSetting.TapButtonMap => "tap_button_map",
        InputSetting.Drag => "drag",
        InputSetting.DragLock => "drag_lock",
        InputSetting.ThreeFingerDrag => "three_finger_drag",
        InputSetting.CalibrationMatrix => "calibration_matrix",
        InputSetting.AccelProfile => "accel_profile",
        InputSetting.AccelSpeed => "accel_speed",
        InputSetting.NaturalScroll => "natural_scroll",
        InputSetting.LeftHanded => "left_handed",
        InputSetting.ClickMethod => "click_method",
        InputSetting.ClickfingerButtonMap => "clickfinger_button_map",
        InputSetting.MiddleEmulation => "middle_emulation",
        InputSetting.ScrollMethod => "scroll_method",
        InputSetting.ScrollButton => "scroll_button",
        InputSetting.ScrollButtonLock => "scroll_button_lock",
        InputSetting.DisableWhileTyping => "dwt",
        InputSetting.DisableWhileTrackpointing => "dwtp",
        InputSetting.Rotation => "rotation",
        InputSetting.ScrollFactor => "scroll_factor",
        InputSetting.MapToOutput => "map_to_output",
        InputSetting.KeymapRules => "rules",
        InputSetting.KeymapModel => "model",
        InputSetting.KeymapLayout => "layout",
        InputSetting.KeymapVariant => "variant",
        InputSetting.KeymapOptions => "options",
        InputSetting.RepeatRate => "repeat_rate",
        InputSetting.RepeatDelay => "repeat_delay",
        _ => throw new ArgumentOutOfRangeException(nameof(setting), setting, null),
    };

    public static bool TryParseKey(ReadOnlySpan<char> text, out InputSetting setting)
    {
        foreach (var candidate in AllSettings)
        {
            if (WordEquals(text, Key(candidate)))
            {
                setting = candidate;
                return true;
            }
        }

        setting = default;
        return false;
    }

    public static string TypeName(InputDeviceType type) => type switch
    {
        InputDeviceType.Keyboard => "keyboard",
        InputDeviceType.Pointer => "pointer",
        InputDeviceType.Touchpad => "touchpad",
        InputDeviceType.Touch => "touch",
        InputDeviceType.TabletTool => "tablet_tool",
        InputDeviceType.TabletPad => "tablet_pad",
        InputDeviceType.Switch => "switch",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    public static bool TryParseType(ReadOnlySpan<char> text, out InputDeviceType type)
    {
        foreach (var candidate in AllTypes)
        {
            if (WordEquals(text, TypeName(candidate)))
            {
                type = candidate;
                return true;
            }
        }

        type = default;
        return false;
    }

    public static string LayerName(InputSettingLayer layer) => layer switch
    {
        InputSettingLayer.Config => "config",
        InputSettingLayer.Runtime => "runtime",
        _ => "default",
    };

    public static InputSettingKind KindOf(InputSetting setting) => setting switch
    {
        InputSetting.Tap or InputSetting.Drag or InputSetting.NaturalScroll or InputSetting.LeftHanded
            or InputSetting.MiddleEmulation or InputSetting.ScrollButtonLock or InputSetting.DisableWhileTyping
            or InputSetting.DisableWhileTrackpointing => InputSettingKind.Flag,
        InputSetting.TapButtonMap or InputSetting.ClickfingerButtonMap or InputSetting.DragLock
            or InputSetting.ThreeFingerDrag or InputSetting.ClickMethod or InputSetting.ScrollMethod
            or InputSetting.AccelProfile or InputSetting.SendEvents => InputSettingKind.Choice,
        InputSetting.ScrollButton or InputSetting.Rotation or InputSetting.RepeatRate
            or InputSetting.RepeatDelay => InputSettingKind.Integer,
        InputSetting.AccelSpeed or InputSetting.ScrollFactor => InputSettingKind.Number,
        InputSetting.CalibrationMatrix => InputSettingKind.Matrix,
        _ => InputSettingKind.Text,
    };

    public static bool IsKeyboard(InputSetting setting) => setting is InputSetting.KeymapRules or InputSetting.KeymapModel
        or InputSetting.KeymapLayout or InputSetting.KeymapVariant or InputSetting.KeymapOptions
        or InputSetting.RepeatRate or InputSetting.RepeatDelay;

    public static bool IsCompositorSide(InputSetting setting) =>
        setting is InputSetting.ScrollFactor or InputSetting.MapToOutput;

    public static bool IsLibinput(InputSetting setting) => !IsKeyboard(setting) && !IsCompositorSide(setting);

    public static ReadOnlySpan<string> Words(InputSetting setting) => setting switch
    {
        InputSetting.TapButtonMap or InputSetting.ClickfingerButtonMap => ButtonMapWords,
        InputSetting.DragLock => DragLockWords,
        InputSetting.ThreeFingerDrag => ThreeFingerDragWords,
        InputSetting.ClickMethod => ClickMethodWords,
        InputSetting.ScrollMethod => ScrollMethodWords,
        InputSetting.AccelProfile => AccelProfileWords,
        InputSetting.SendEvents => SendEventsWords,
        _ when KindOf(setting) == InputSettingKind.Flag => FlagWords,
        _ => [],
    };

    public static bool IsSupportedWord(InputSetting setting, string word, in InputSettingSupport support)
    {
        if (!support.Supported || !TryParse(setting, word, out var value))
        {
            return false;
        }

        return setting switch
        {
            InputSetting.ClickMethod or InputSetting.ScrollMethod or InputSetting.AccelProfile or InputSetting.SendEvents =>
                value.Value == 0 || (support.Mask & value.Value) != 0,
            InputSetting.ThreeFingerDrag => value.Value == 0 || (value.Value == 1 ? support.Count >= 3 : support.Count >= 4),
            _ => true,
        };
    }

    public static bool TryParse(InputSetting setting, ReadOnlySpan<char> text, out InputSettingValue value)
    {
        value = default;
        text = text.Trim();
        switch (KindOf(setting))
        {
            case InputSettingKind.Flag:
                if (WordEquals(text, "true") || WordEquals(text, "enabled"))
                {
                    value = InputSettingValue.Flag(true);
                    return true;
                }

                if (WordEquals(text, "false") || WordEquals(text, "disabled"))
                {
                    value = InputSettingValue.Flag(false);
                    return true;
                }

                return false;

            case InputSettingKind.Choice:
                return TryParseChoice(setting, text, out value);

            case InputSettingKind.Integer:
                if (!uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var integer))
                {
                    return false;
                }

                if (setting == InputSetting.Rotation && integer >= 360)
                {
                    return false;
                }

                value = InputSettingValue.Of(integer);
                return true;

            case InputSettingKind.Number:
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
                {
                    return false;
                }

                if ((setting == InputSetting.AccelSpeed && number is < -1 or > 1) ||
                    (setting == InputSetting.ScrollFactor && number <= 0))
                {
                    return false;
                }

                value = InputSettingValue.Number(number);
                return true;

            case InputSettingKind.Matrix:
                Span<double> numbers = stackalloc double[InputSettingValue.MaxNumbers];
                var count = 0;
                foreach (var range in text.SplitAny(" ,\t"))
                {
                    var part = text[range];
                    if (part.IsEmpty)
                    {
                        continue;
                    }

                    if (count == numbers.Length ||
                        !double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[count]) ||
                        !double.IsFinite(numbers[count]))
                    {
                        return false;
                    }

                    count++;
                }

                if (count != numbers.Length)
                {
                    return false;
                }

                value = InputSettingValue.Matrix(numbers);
                return true;

            default:
                value = InputSettingValue.Text(text.ToString());
                return true;
        }
    }

    public static string Format(InputSetting setting, in InputSettingValue value)
    {
        switch (KindOf(setting))
        {
            case InputSettingKind.Flag:
                return value.IsOn ? "true" : "false";
            case InputSettingKind.Choice:
                return ChoiceWord(setting, value.Value) ?? value.Value.ToString(CultureInfo.InvariantCulture);
            case InputSettingKind.Integer:
                return value.Value.ToString(CultureInfo.InvariantCulture);
            case InputSettingKind.Number:
                return value.Count > 0 ? value[0].ToString("R", CultureInfo.InvariantCulture) : "0";
            case InputSettingKind.Matrix:
                var builder = new System.Text.StringBuilder();
                for (var i = 0; i < value.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(' ');
                    }

                    builder.Append(value[i].ToString("R", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            default:
                return value.TextValue ?? string.Empty;
        }
    }

    public static bool WordEquals(ReadOnlySpan<char> text, string canonical)
    {
        if (text.Length != canonical.Length)
        {
            return false;
        }

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i] == '-' ? '_' : char.ToLowerInvariant(text[i]);
            if (c != canonical[i])
            {
                return false;
            }
        }

        return true;
    }

    private static string? ChoiceWord(InputSetting setting, uint raw)
    {
        var words = Words(setting);
        for (var i = 0; i < words.Length; i++)
        {
            if (TryParseChoice(setting, words[i], out var candidate) && candidate.Value == raw)
            {
                return words[i];
            }
        }

        return null;
    }

    private static bool TryParseChoice(InputSetting setting, ReadOnlySpan<char> text, out InputSettingValue value)
    {
        uint? raw = setting switch
        {
            InputSetting.TapButtonMap or InputSetting.ClickfingerButtonMap =>
                WordEquals(text, "lrm") ? 0u : WordEquals(text, "lmr") ? 1u : null,
            InputSetting.DragLock =>
                WordEquals(text, "disabled") || WordEquals(text, "false") ? 0u
                : WordEquals(text, "enabled") || WordEquals(text, "true") ? 1u
                : WordEquals(text, "enabled_sticky") ? 2u : null,
            InputSetting.ThreeFingerDrag =>
                WordEquals(text, "disabled") || WordEquals(text, "false") ? 0u
                : WordEquals(text, "enabled_3fg") ? 1u
                : WordEquals(text, "enabled_4fg") ? 2u : null,
            InputSetting.ClickMethod =>
                WordEquals(text, "none") ? 0u
                : WordEquals(text, "button_areas") ? 1u
                : WordEquals(text, "clickfinger") ? 2u : null,
            InputSetting.ScrollMethod =>
                WordEquals(text, "none") ? 0u
                : WordEquals(text, "two_finger") ? 1u
                : WordEquals(text, "edge") ? 2u
                : WordEquals(text, "on_button_down") ? 4u : null,
            InputSetting.AccelProfile =>
                WordEquals(text, "none") ? 0u
                : WordEquals(text, "flat") ? 1u
                : WordEquals(text, "adaptive") ? 2u
                : WordEquals(text, "custom") ? 4u : null,
            InputSetting.SendEvents =>
                WordEquals(text, "enabled") || WordEquals(text, "true") ? 0u
                : WordEquals(text, "disabled") || WordEquals(text, "false") ? 1u
                : WordEquals(text, "disabled_on_external_mouse") ? 2u : null,
            _ => null,
        };
        value = raw is { } v ? InputSettingValue.Of(v) : default;
        return raw is not null;
    }
}
