namespace Basin.SoftKeyboard;

public readonly record struct FieldState(bool Active, FieldKind Kind, bool AutoCap, uint Hint, uint Purpose)
{
    public const uint HintAutoCapitalization = 0x4;
    public const uint HintLowercase = 0x8;
    public const uint HintUppercase = 0x10;

    public static FieldState Inactive => default;

    public BottomRow BottomRow => Kind switch
    {
        FieldKind.Email => BottomRow.Email,
        FieldKind.Url => BottomRow.Url,
        _ => BottomRow.Default,
    };

    public bool NumLock => Kind is FieldKind.Number or FieldKind.Phone;

    public static FieldKind KindOf(uint purpose) => purpose switch
    {
        0 or 1 or 7 => FieldKind.Text,
        2 or 3 or 9 => FieldKind.Number,
        4 => FieldKind.Phone,
        5 => FieldKind.Url,
        6 => FieldKind.Email,
        8 => FieldKind.Password,
        10 or 11 or 12 => FieldKind.Date,
        13 => FieldKind.Terminal,
        _ => FieldKind.Text,
    };

    public static bool AutoCapFor(uint hint, uint purpose, string? surrounding, int cursorBytes)
    {
        if ((hint & HintLowercase) != 0)
        {
            return false;
        }

        if ((hint & HintUppercase) != 0)
        {
            return true;
        }

        if ((hint & HintAutoCapitalization) == 0 || purpose is not (0 or 1 or 7))
        {
            return false;
        }

        if (string.IsNullOrEmpty(surrounding) || cursorBytes <= 0)
        {
            return true;
        }

        var before = BeforeCursor(surrounding, cursorBytes);
        if (before.IsEmpty || before[^1] == '\n')
        {
            return true;
        }

        return before.Length >= 2 && before[^1] == ' ' && before[^2] is '.' or '!' or '?';
    }

    public static ReadOnlySpan<char> BeforeCursor(string text, int cursorBytes)
    {
        ArgumentNullException.ThrowIfNull(text);
        var bytes = 0;
        var index = 0;
        while (index < text.Length && bytes < cursorBytes)
        {
            if (char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            {
                bytes += 4;
                index += 2;
                continue;
            }

            bytes += text[index] switch
            {
                < '\u0080' => 1,
                < '\u0800' => 2,
                _ => 3,
            };
            index++;
        }

        return text.AsSpan(0, index);
    }
}
