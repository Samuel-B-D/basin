using System.Xml;

namespace Basin.Freedesktop;

public sealed class MimeGlobs
{
    public const string Directory = "inode/directory";

    public const string Unknown = "application/octet-stream";

    private readonly IReadOnlyList<string> _dataDirectories;
    private readonly List<MimeGlob> _literals = [];
    private readonly Dictionary<string, List<MimeGlob>> _suffixes = new(StringComparer.Ordinal);
    private readonly List<MimeGlob> _patterns = [];
    private readonly Dictionary<string, string> _icons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _genericIcons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _aliases = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> _comments = new(StringComparer.Ordinal);
    private bool _loaded;

    public MimeGlobs(IReadOnlyList<string>? dataDirectories = null)
    {
        _dataDirectories = dataDirectories ?? XdgDirectories.DataDirectories();
    }

    public string TypeOf(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (System.IO.Directory.Exists(path))
        {
            return Directory;
        }

        return TypeOfName(Path.GetFileName(path)) ?? Unknown;
    }

    public string? TypeOfName(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        Load();
        if (Best(_literals, g => Matches(g, fileName, literal: true)) is { } literal)
        {
            return Canonical(literal.MimeType);
        }

        MimeGlob? best = null;
        for (var dot = fileName.IndexOf('.'); dot >= 0; dot = fileName.IndexOf('.', dot + 1))
        {
            var suffix = fileName[(dot + 1)..].ToLowerInvariant();
            if (_suffixes.TryGetValue(suffix, out var candidates) &&
                Best(candidates, g => Matches(g, fileName, literal: false)) is { } found &&
                (best is not { } current || found.Weight > current.Weight || (found.Weight == current.Weight && found.Pattern.Length > current.Pattern.Length)))
            {
                best = found;
            }
        }

        if (best is { } suffixMatch)
        {
            return Canonical(suffixMatch.MimeType);
        }

        return Best(_patterns, g => Matches(g, fileName, literal: false)) is { } pattern ? Canonical(pattern.MimeType) : null;
    }

    public IReadOnlyList<string> IconNamesOf(string mimeType)
    {
        ArgumentNullException.ThrowIfNull(mimeType);
        Load();
        mimeType = Canonical(mimeType);
        var names = new List<string>(3);
        names.Add(_icons.TryGetValue(mimeType, out var icon) ? icon : mimeType.Replace('/', '-'));
        if (_genericIcons.TryGetValue(mimeType, out var generic))
        {
            names.Add(generic);
        }

        var slash = mimeType.IndexOf('/');
        var fallback = (slash > 0 ? mimeType[..slash] : mimeType) + "-x-generic";
        if (!names.Contains(fallback))
        {
            names.Add(fallback);
        }

        return names;
    }

    public string? CommentOf(string mimeType)
    {
        ArgumentNullException.ThrowIfNull(mimeType);
        Load();
        mimeType = Canonical(mimeType);
        if (_comments.TryGetValue(mimeType, out var cached))
        {
            return cached;
        }

        string? comment = null;
        foreach (var directory in _dataDirectories)
        {
            var file = Path.Combine(directory, "mime", mimeType + ".xml");
            if (File.Exists(file) && ReadComment(file) is { } found)
            {
                comment = found;
                break;
            }
        }

        _comments[mimeType] = comment;
        return comment;
    }

    public void Invalidate()
    {
        _loaded = false;
        _literals.Clear();
        _suffixes.Clear();
        _patterns.Clear();
        _icons.Clear();
        _genericIcons.Clear();
        _aliases.Clear();
        _comments.Clear();
    }

    private string Canonical(string mimeType) => _aliases.TryGetValue(mimeType, out var canonical) ? canonical : mimeType;

    private static MimeGlob? Best(List<MimeGlob> globs, Func<MimeGlob, bool> matches)
    {
        MimeGlob? best = null;
        foreach (var glob in globs)
        {
            if (!matches(glob))
            {
                continue;
            }

            if (best is not { } current || glob.Weight > current.Weight ||
                (glob.Weight == current.Weight && glob.Pattern.Length > current.Pattern.Length))
            {
                best = glob;
            }
        }

        return best;
    }

    private static bool Matches(MimeGlob glob, string name, bool literal)
    {
        var comparison = glob.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        if (literal)
        {
            return string.Equals(glob.Pattern, name, comparison);
        }

        return glob.Pattern.StartsWith("*.", StringComparison.Ordinal) && glob.Pattern.IndexOfAny(['*', '?', '['], 1) < 0
            ? name.EndsWith(glob.Pattern[1..], comparison)
            : GlobMatch(glob.Pattern, name, glob.CaseSensitive);
    }

    private static bool GlobMatch(string pattern, string name, bool caseSensitive)
    {
        if (!caseSensitive)
        {
            pattern = pattern.ToLowerInvariant();
            name = name.ToLowerInvariant();
        }

        return GlobMatch(pattern.AsSpan(), name.AsSpan());
    }

    private static bool GlobMatch(ReadOnlySpan<char> pattern, ReadOnlySpan<char> name)
    {
        while (pattern.Length > 0)
        {
            switch (pattern[0])
            {
                case '*':
                    for (var skip = 0; skip <= name.Length; skip++)
                    {
                        if (GlobMatch(pattern[1..], name[skip..]))
                        {
                            return true;
                        }
                    }

                    return false;
                case '?':
                    if (name.IsEmpty)
                    {
                        return false;
                    }

                    pattern = pattern[1..];
                    name = name[1..];
                    break;
                case '[':
                    var close = pattern.IndexOf(']');
                    if (close < 0 || name.IsEmpty)
                    {
                        return false;
                    }

                    var set = pattern[1..close];
                    var negate = set.Length > 0 && set[0] == '!';
                    if (negate)
                    {
                        set = set[1..];
                    }

                    var hit = false;
                    for (var i = 0; i < set.Length; i++)
                    {
                        if (i + 2 < set.Length && set[i + 1] == '-')
                        {
                            hit |= name[0] >= set[i] && name[0] <= set[i + 2];
                            i += 2;
                        }
                        else
                        {
                            hit |= name[0] == set[i];
                        }
                    }

                    if (hit == negate)
                    {
                        return false;
                    }

                    pattern = pattern[(close + 1)..];
                    name = name[1..];
                    break;
                default:
                    if (name.IsEmpty || name[0] != pattern[0])
                    {
                        return false;
                    }

                    pattern = pattern[1..];
                    name = name[1..];
                    break;
            }
        }

        return name.IsEmpty;
    }

    private void Load()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        var globs = new Dictionary<string, List<MimeGlob>>(StringComparer.Ordinal);
        for (var i = _dataDirectories.Count - 1; i >= 0; i--)
        {
            var mime = Path.Combine(_dataDirectories[i], "mime");
            ReadGlobs(Path.Combine(mime, "globs2"), globs);
            ReadPairs(Path.Combine(mime, "icons"), _icons);
            ReadPairs(Path.Combine(mime, "generic-icons"), _genericIcons);
            ReadPairs(Path.Combine(mime, "aliases"), _aliases, spaced: true);
        }

        foreach (var list in globs.Values)
        {
            foreach (var glob in list)
            {
                if (glob.Pattern.IndexOfAny(['*', '?', '[']) < 0)
                {
                    _literals.Add(glob);
                }
                else if (glob.Pattern.StartsWith("*.", StringComparison.Ordinal) && glob.Pattern.IndexOfAny(['*', '?', '['], 1) < 0)
                {
                    var suffix = glob.Pattern[2..].ToLowerInvariant();
                    if (!_suffixes.TryGetValue(suffix, out var bucket))
                    {
                        bucket = [];
                        _suffixes[suffix] = bucket;
                    }

                    bucket.Add(glob);
                }
                else
                {
                    _patterns.Add(glob);
                }
            }
        }
    }

    private static void ReadGlobs(string file, Dictionary<string, List<MimeGlob>> globs)
    {
        if (!File.Exists(file))
        {
            return;
        }

        foreach (var line in File.ReadLines(file))
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var fields = line.Split(':');
            if (fields.Length < 3 || !int.TryParse(fields[0], out var weight))
            {
                continue;
            }

            var type = fields[1];
            var pattern = fields[2];
            if (pattern == "__NOGLOBS__")
            {
                globs.Remove(type);
                continue;
            }

            var caseSensitive = fields.Length > 3 && fields[3].Split(',').Contains("cs");
            if (!globs.TryGetValue(type, out var list))
            {
                list = [];
                globs[type] = list;
            }

            list.Add(new MimeGlob(weight, type, pattern, caseSensitive));
        }
    }

    private static void ReadPairs(string file, Dictionary<string, string> table, bool spaced = false)
    {
        if (!File.Exists(file))
        {
            return;
        }

        foreach (var line in File.ReadLines(file))
        {
            var split = spaced ? line.IndexOf(' ') : line.IndexOf(':');
            if (split > 0 && line[0] != '#')
            {
                table[line[..split]] = line[(split + 1)..].Trim();
            }
        }
    }

    private static string? ReadComment(string file)
    {
        try
        {
            using var reader = XmlReader.Create(file, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null });
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "comment" && reader.GetAttribute("xml:lang") is null)
                {
                    return reader.ReadElementContentAsString();
                }
            }
        }
        catch (Exception error) when (error is XmlException or IOException or UnauthorizedAccessException)
        {
        }

        return null;
    }
}
