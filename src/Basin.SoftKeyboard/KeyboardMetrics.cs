namespace Basin.SoftKeyboard;

public readonly record struct KeyboardMetrics(int Height, int KeymapHeight, int Padding)
{
    public const int StatusBarReserve = 28;
    public const int CandidateBarHeight = 55;
    public const int FullHeight = 340;
    public const int FullKeymapHeight = 335;

    public static int PresetHeight(KeyboardSize size) => size switch
    {
        KeyboardSize.ExtraSmall => 243,
        KeyboardSize.Small => (340 + 243) / 2,
        KeyboardSize.Large => 393,
        _ => FullHeight,
    };

    public static KeyboardMetrics For(KeyboardSize size, int outputWidth, int outputHeight, bool sizeChosen = true)
    {
        var height = PresetHeight(size);
        if (outputHeight > outputWidth && !sizeChosen)
        {
            height = Math.Min(outputWidth * 200 / 320, 300);
        }

        return ForHeight(height, outputHeight);
    }

    public static KeyboardMetrics ForHeight(int height, int outputHeight)
    {
        var max = outputHeight - StatusBarReserve;
        if (height > max)
        {
            height = max;
        }

        height = Math.Max(height, 1);
        var keymap = height * FullKeymapHeight / FullHeight;
        return new KeyboardMetrics(height, keymap, Math.Max(height - keymap, 0));
    }
}
