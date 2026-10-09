using System.Globalization;

namespace Basin.SoftKeyboard;

public sealed class KeyCap
{
    public static readonly KeyCap Backspace = new(KeyFunction.Backspace, "backspace");
    public static readonly KeyCap Return = new(KeyFunction.Return, "return");
    public static readonly KeyCap Shift = new(KeyFunction.Shift, "shift");
    public static readonly KeyCap Symbol = new(KeyFunction.Symbol, "symbol");
    public static readonly KeyCap Hide = new(KeyFunction.Hide, "hide");
    public static readonly KeyCap Tab = new(KeyFunction.Tab, "tab");
    public static readonly KeyCap Left = new(KeyFunction.Left, "left");
    public static readonly KeyCap Right = new(KeyFunction.Right, "right");
    public static readonly KeyCap Language = new(KeyFunction.Language, "language");
    public static readonly KeyCap Space = Character(" ");
    public static readonly KeyCap ExtraSmall = SizeCap(KeyboardSize.ExtraSmall, "XS");
    public static readonly KeyCap Small = SizeCap(KeyboardSize.Small, "S");
    public static readonly KeyCap Medium = SizeCap(KeyboardSize.Medium, "M");
    public static readonly KeyCap Large = SizeCap(KeyboardSize.Large, "L");

    private KeyCap(KeyFunction function, string name, string? label = null, int argument = 0)
    {
        Function = function;
        Name = name;
        Text = string.Empty;
        UpperText = string.Empty;
        Label = label;
        Argument = argument;
    }

    private KeyCap(string text, string? label, bool? letter = null)
    {
        Function = KeyFunction.Text;
        Name = text;
        Text = text;
        UpperText = text.Length == 1 ? text.ToUpper(CultureInfo.InvariantCulture) : text;
        Label = label;
        IsLetter = letter ?? (text.Length == 1 && IsPagedLetter(text[0]));
    }

    public KeyFunction Function { get; }

    public string Name { get; }

    public string Text { get; }

    public string UpperText { get; }

    public string? Label { get; }

    public int Argument { get; }

    public bool IsLetter { get; }

    public bool IsText => Function == KeyFunction.Text;

    public bool IsShortcut => Function == KeyFunction.Text && Text.Length > 1;

    public bool IsSpace => Function == KeyFunction.Text && Text == " ";

    public bool IsDigit => Function == KeyFunction.Text && Text.Length == 1 && Text[0] is >= '0' and <= '9';

    public bool Repeats => Function is KeyFunction.Backspace or KeyFunction.Left or KeyFunction.Right || IsSpace;

    public KeyboardSize Size => (KeyboardSize)Argument;

    public static KeyCap Character(string text, string? label = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        return new KeyCap(text, label);
    }

    public static KeyCap Character(string text, string? label, bool letter)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        return new KeyCap(text, label, letter);
    }

    public static KeyCap LanguageKey(string label) => new(KeyFunction.Language, "language", label);

    public static KeyCap LanguageChoice(int index, string label) => new(KeyFunction.LanguageChoice, "language-" + index.ToString(CultureInfo.InvariantCulture), label, index);

    public static KeyCap SizeCap(KeyboardSize size) => size switch
    {
        KeyboardSize.ExtraSmall => ExtraSmall,
        KeyboardSize.Small => Small,
        KeyboardSize.Large => Large,
        _ => Medium,
    };

    public static bool TryParse(string token, out KeyCap? cap)
    {
        ArgumentNullException.ThrowIfNull(token);
        cap = null;
        if (token.Length > 2 && token[0] == '{' && token[^1] == '}')
        {
            cap = token[1..^1] switch
            {
                "backspace" => Backspace,
                "return" => Return,
                "shift" => Shift,
                "symbol" => Symbol,
                "hide" => Hide,
                "tab" => Tab,
                "left" => Left,
                "right" => Right,
                "language" => Language,
                "size-xs" => ExtraSmall,
                "size-s" => Small,
                "size-m" => Medium,
                "size-l" => Large,
                _ => null,
            };
            return cap is not null;
        }

        if (token.Length == 0)
        {
            return false;
        }

        cap = token == " " ? Space : Character(token);
        return true;
    }

    public override string ToString() => Function == KeyFunction.Text ? Text : Name;

    private static KeyCap SizeCap(KeyboardSize size, string label) =>
        new(KeyFunction.Size, "size-" + label.ToLowerInvariant(), label, (int)size);

    private static bool IsPagedLetter(char c) => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or 'ä' or 'ö' or 'å' or 'Ä' or 'Ö' or 'Å';
}
