namespace Basin.Themes.MsStyles;

internal sealed class MsStylesIni
{
    private MsStylesIni(List<MsStylesIniSection> sections)
    {
        Sections = sections;
    }

    public IReadOnlyList<MsStylesIniSection> Sections { get; }

    public static MsStylesIni Parse(ReadOnlySpan<char> text, MsStylesDiagnostics? diagnostics = null)
    {
        var sections = new List<MsStylesIniSection>();
        MsStylesIniSection? current = null;
        if (text.Length > 0 && text[0] == '﻿')
        {
            text = text[1..];
        }

        while (text.Length > 0)
        {
            var newline = text.IndexOf('\n');
            var line = newline < 0 ? text : text[..newline];
            text = newline < 0 ? ReadOnlySpan<char>.Empty : text[(newline + 1)..];

            var comment = line.IndexOf(';');
            if (comment >= 0)
            {
                line = line[..comment];
            }

            line = line.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line[0] == '[')
            {
                var close = line.LastIndexOf(']');
                var name = (close > 0 ? line[1..close] : line[1..]).Trim().ToString();
                current = new MsStylesIniSection(name);
                sections.Add(current);
                continue;
            }

            var equals = line.IndexOf('=');
            if (equals <= 0 || current is null)
            {
                diagnostics?.Add("ini-line", line.ToString());
                continue;
            }

            current.Values.Add((line[..equals].Trim().ToString(), line[(equals + 1)..].Trim().ToString()));
        }

        return new MsStylesIni(sections);
    }
}
