namespace Basin.SoftKeyboard;

internal sealed class HunspellAffixes
{
    private readonly Dictionary<string, AffixGroup> _prefixes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AffixGroup> _suffixes = new(StringComparer.Ordinal);
    private readonly HashSet<string> _skip = new(StringComparer.Ordinal);
    private string? _needAffix;
    private FlagKind _flags = FlagKind.Single;

    private enum FlagKind
    {
        Single,
        Long,
        Number,
        Utf8,
    }

    public static HunspellAffixes Parse(string aff)
    {
        var affixes = new HunspellAffixes();
        AffixGroup? group = null;
        var remaining = 0;
        foreach (var raw in aff.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            switch (fields[0])
            {
                case "FLAG" when fields.Length > 1:
                    affixes._flags = fields[1] switch
                    {
                        "long" => FlagKind.Long,
                        "num" => FlagKind.Number,
                        "UTF-8" => FlagKind.Utf8,
                        _ => FlagKind.Single,
                    };
                    continue;
                case "NOSUGGEST" or "FORBIDDENWORD" or "ONLYINCOMPOUND" when fields.Length > 1:
                    affixes._skip.Add(fields[1]);
                    continue;
                case "NEEDAFFIX" when fields.Length > 1:
                    affixes._needAffix = fields[1];
                    continue;
                case "PFX" or "SFX" when fields.Length >= 4 && remaining == 0 && int.TryParse(fields[3], out var count):
                    group = new AffixGroup(fields[0] == "PFX", fields[2] == "Y");
                    (group.Prefix ? affixes._prefixes : affixes._suffixes)[fields[1]] = group;
                    remaining = count;
                    continue;
                case "PFX" or "SFX" when fields.Length >= 4 && remaining > 0 && group is not null:
                    remaining--;
                    var strip = fields[2] == "0" ? string.Empty : fields[2];
                    var add = fields[3];
                    var slash = add.IndexOf('/');
                    var continuation = string.Empty;
                    if (slash >= 0)
                    {
                        continuation = add[(slash + 1)..];
                        add = add[..slash];
                    }

                    if (add == "0")
                    {
                        add = string.Empty;
                    }

                    var condition = fields.Length > 4 ? fields[4] : ".";
                    group.Rules.Add(new AffixRule(strip, add, AffixCondition.Parse(condition), continuation));
                    continue;
            }
        }

        return affixes;
    }

    public void Expand(string line, Action<string> emit)
    {
        var entry = line.Trim();
        if (entry.Length == 0)
        {
            return;
        }

        var tab = entry.IndexOf('\t');
        if (tab >= 0)
        {
            entry = entry[..tab];
        }

        var slash = FindFlagSlash(entry);
        var stem = slash >= 0 ? entry[..slash].Replace("\\/", "/", StringComparison.Ordinal) : entry;
        var flags = slash >= 0 ? SplitFlags(entry[(slash + 1)..]) : [];
        foreach (var flag in flags)
        {
            if (_skip.Contains(flag))
            {
                return;
            }
        }

        if (stem.Length == 0 || char.IsDigit(stem[0]))
        {
            return;
        }

        if (_needAffix is null || Array.IndexOf(flags, _needAffix) < 0)
        {
            emit(stem);
        }

        var prefixes = new List<(AffixGroup Group, AffixRule Rule)>();
        foreach (var flag in flags)
        {
            if (_prefixes.TryGetValue(flag, out var group))
            {
                foreach (var rule in group.Rules)
                {
                    if (rule.Condition.MatchesStart(stem) && stem.StartsWith(rule.Strip, StringComparison.Ordinal))
                    {
                        emit(rule.Add + stem[rule.Strip.Length..]);
                        prefixes.Add((group, rule));
                    }
                }
            }
        }

        foreach (var flag in flags)
        {
            if (!_suffixes.TryGetValue(flag, out var group))
            {
                continue;
            }

            foreach (var rule in group.Rules)
            {
                if (!rule.Condition.MatchesEnd(stem) || !stem.EndsWith(rule.Strip, StringComparison.Ordinal))
                {
                    continue;
                }

                var suffixed = stem[..^rule.Strip.Length] + rule.Add;
                emit(suffixed);
                if (!group.Cross)
                {
                    continue;
                }

                foreach (var (prefixGroup, prefix) in prefixes)
                {
                    if (prefixGroup.Cross && suffixed.StartsWith(prefix.Strip, StringComparison.Ordinal))
                    {
                        emit(prefix.Add + suffixed[prefix.Strip.Length..]);
                    }
                }
            }
        }
    }

    private static int FindFlagSlash(string entry)
    {
        for (var i = 1; i < entry.Length; i++)
        {
            if (entry[i] == '/' && entry[i - 1] != '\\')
            {
                return i;
            }
        }

        return -1;
    }

    private string[] SplitFlags(string text)
    {
        switch (_flags)
        {
            case FlagKind.Long:
                var pairs = new string[text.Length / 2];
                for (var i = 0; i < pairs.Length; i++)
                {
                    pairs[i] = text.Substring(i * 2, 2);
                }

                return pairs;
            case FlagKind.Number:
                return text.Split(',', StringSplitOptions.RemoveEmptyEntries);
            default:
                var flags = new List<string>(text.Length);
                foreach (var rune in text.EnumerateRunes())
                {
                    flags.Add(rune.ToString());
                }

                return [.. flags];
        }
    }

    private sealed class AffixGroup(bool prefix, bool cross)
    {
        public bool Prefix { get; } = prefix;

        public bool Cross { get; } = cross;

        public List<AffixRule> Rules { get; } = [];
    }

    private sealed record AffixRule(string Strip, string Add, AffixCondition Condition, string Continuation);

    private sealed class AffixCondition
    {
        private readonly List<(bool Negated, string Set)> _parts = [];

        public static AffixCondition Parse(string text)
        {
            var condition = new AffixCondition();
            if (text == ".")
            {
                return condition;
            }

            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '[')
                {
                    var close = text.IndexOf(']', i + 1);
                    if (close < 0)
                    {
                        break;
                    }

                    var body = text[(i + 1)..close];
                    var negated = body.StartsWith('^');
                    condition._parts.Add((negated, negated ? body[1..] : body));
                    i = close;
                }
                else
                {
                    condition._parts.Add((false, text[i] == '.' ? string.Empty : text[i].ToString()));
                }
            }

            return condition;
        }

        public bool MatchesEnd(string word)
        {
            if (_parts.Count > word.Length)
            {
                return false;
            }

            var start = word.Length - _parts.Count;
            for (var i = 0; i < _parts.Count; i++)
            {
                if (!Matches(_parts[i], word[start + i]))
                {
                    return false;
                }
            }

            return true;
        }

        public bool MatchesStart(string word)
        {
            if (_parts.Count > word.Length)
            {
                return false;
            }

            for (var i = 0; i < _parts.Count; i++)
            {
                if (!Matches(_parts[i], word[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Matches((bool Negated, string Set) part, char c) =>
            part.Set.Length == 0 || (part.Set.Contains(c) != part.Negated);
    }
}
