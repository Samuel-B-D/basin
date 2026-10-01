namespace Basin.Themes.MsStyles;

internal static class MsStylesValueParser
{
    public const int MaxIntList = 402;

    public static bool TryNextInteger(ReadOnlySpan<char> text, ref int position, out int value)
    {
        value = 0;
        var cursor = position;
        while (cursor < text.Length && !IsDigit(text[cursor]) && !(text[cursor] == '-' && cursor + 1 < text.Length && IsDigit(text[cursor + 1])))
        {
            cursor++;
        }

        if (cursor >= text.Length)
        {
            return false;
        }

        var negative = false;
        if (text[cursor] == '-')
        {
            negative = true;
            cursor++;
        }

        long total = 0;
        while (cursor < text.Length && IsDigit(text[cursor]))
        {
            total = total * 10 + (text[cursor] - '0');
            if (total > int.MaxValue)
            {
                total = int.MaxValue;
            }

            cursor++;
        }

        value = (int)(negative ? -total : total);
        position = cursor;
        return true;
    }

    public static int[] Integers(ReadOnlySpan<char> text, int max)
    {
        Span<int> buffer = stackalloc int[MaxIntList];
        var count = 0;
        var position = 0;
        while (count < Math.Min(max, MaxIntList) && TryNextInteger(text, ref position, out var value))
        {
            buffer[count++] = value;
        }

        return buffer[..count].ToArray();
    }

    public static bool ParseBool(ReadOnlySpan<char> text) =>
        text.Length > 0 && (text[0] == 't' || text[0] == 'T');

    public static bool ParseLocalizedBool(ReadOnlySpan<char> text)
    {
        var trimmed = text.Trim();
        return ParseBool(trimmed) || (trimmed.Length > 0 && trimmed[0] is >= '1' and <= '9');
    }

    public static bool TryParseFont(ReadOnlySpan<char> text, out MsStylesFont font)
    {
        font = default;
        var comma = text.IndexOf(',');
        var face = (comma < 0 ? text : text[..comma]).Trim();
        if (face.Length == 0 || comma < 0)
        {
            return false;
        }

        var rest = text[(comma + 1)..];
        var position = 0;
        if (!TryNextInteger(rest, ref position, out var points))
        {
            return false;
        }

        var weight = MsStylesFont.Regular;
        var italic = false;
        var underline = false;
        var strikeOut = false;
        var attributes = rest[position..];
        while (attributes.Length > 0)
        {
            var next = attributes.IndexOf(',');
            var token = (next < 0 ? attributes : attributes[..next]).Trim();
            if (token.Equals("bold", StringComparison.OrdinalIgnoreCase))
            {
                weight = MsStylesFont.Bold;
            }
            else if (token.Equals("italic", StringComparison.OrdinalIgnoreCase))
            {
                italic = true;
            }
            else if (token.Equals("underline", StringComparison.OrdinalIgnoreCase))
            {
                underline = true;
            }
            else if (token.Equals("strikeout", StringComparison.OrdinalIgnoreCase))
            {
                strikeOut = true;
            }

            if (next < 0)
            {
                break;
            }

            attributes = attributes[(next + 1)..];
        }

        font = new MsStylesFont(face.ToString(), points, weight, italic, underline, strikeOut);
        return true;
    }

    public static string ResourceNameOf(ReadOnlySpan<char> path)
    {
        Span<char> name = path.Length <= 256 ? stackalloc char[path.Length] : new char[path.Length];
        for (var i = 0; i < path.Length; i++)
        {
            var c = path[i];
            name[i] = c is '\\' or '/' or '.' ? '_' : char.ToUpperInvariant(c);
        }

        return name.Trim().ToString();
    }

    private static bool IsDigit(char c) => c is >= '0' and <= '9';
}
