using System.Text;

namespace System7Wm;

internal static class ShellWords
{
    public static string[] Split(string text)
    {
        var words = new List<string>();
        var current = new StringBuilder();
        var started = false;
        var quote = '\0';
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quote == '\'')
            {
                if (c == '\'')
                {
                    quote = '\0';
                }
                else
                {
                    current.Append(c);
                }

                continue;
            }

            if (c == '\\' && i + 1 < text.Length && (quote == '\0' || text[i + 1] is '"' or '\\' or '$' or '`'))
            {
                current.Append(text[++i]);
                started = true;
                continue;
            }

            if (quote == '"')
            {
                if (c == '"')
                {
                    quote = '\0';
                }
                else
                {
                    current.Append(c);
                }

                continue;
            }

            if (c is '\'' or '"')
            {
                quote = c;
                started = true;
            }
            else if (char.IsWhiteSpace(c))
            {
                if (started)
                {
                    words.Add(current.ToString());
                    current.Clear();
                    started = false;
                }
            }
            else
            {
                current.Append(c);
                started = true;
            }
        }

        if (started)
        {
            words.Add(current.ToString());
        }

        return [.. words];
    }
}
