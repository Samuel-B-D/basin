namespace Basin.Capabilities;

public readonly record struct InputMatch(
    InputDeviceType? Type = null,
    string? NameGlob = null,
    string? Sysname = null,
    uint? Vendor = null,
    uint? Product = null,
    string? Output = null,
    bool AnyKeyboard = false)
{
    public bool IsCatchAll => Type is null && NameGlob is null && Sysname is null && Vendor is null && Product is null &&
        Output is null && !AnyKeyboard;

    public bool NamesDevice => NameGlob is not null || Sysname is not null || Vendor is not null || Product is not null || Output is not null;

    public int Specificity =>
        (Vendor is not null ? 32 : 0) +
        (Product is not null ? 32 : 0) +
        (Sysname is not null ? 16 : 0) +
        (NameGlob is not null ? 8 : 0) +
        (Type is not null || AnyKeyboard ? 4 : 0) +
        (Output is not null ? 2 : 0);

    public bool Matches(in InputDeviceInfo device)
    {
        if (Type is { } type && device.Type != type)
        {
            return false;
        }

        if (AnyKeyboard && (device.Capabilities & InputDeviceCapability.Keyboard) == 0)
        {
            return false;
        }

        if (Vendor is { } vendor && device.Vendor != vendor)
        {
            return false;
        }

        if (Product is { } product && device.Product != product)
        {
            return false;
        }

        if (Sysname is { } sysname && !string.Equals(sysname, device.Sysname, StringComparison.Ordinal))
        {
            return false;
        }

        if (Output is { } output && !string.Equals(output, device.OutputName, StringComparison.Ordinal))
        {
            return false;
        }

        return NameGlob is not { } glob || Glob(glob, device.Name);
    }

    public bool MatchesKeyboardFallback() => !NamesDevice && (Type is null or InputDeviceType.Keyboard || AnyKeyboard);

    public static bool Glob(ReadOnlySpan<char> pattern, ReadOnlySpan<char> text)
    {
        var p = 0;
        var t = 0;
        var star = -1;
        var mark = 0;
        while (t < text.Length)
        {
            if (p < pattern.Length && (pattern[p] == '?' || pattern[p] == text[t]))
            {
                p++;
                t++;
            }
            else if (p < pattern.Length && pattern[p] == '*')
            {
                star = p++;
                mark = t;
            }
            else if (star >= 0)
            {
                p = star + 1;
                t = ++mark;
            }
            else
            {
                return false;
            }
        }

        while (p < pattern.Length && pattern[p] == '*')
        {
            p++;
        }

        return p == pattern.Length;
    }
}
