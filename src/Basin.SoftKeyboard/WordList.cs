using System.Buffers.Binary;
using System.Text;

namespace Basin.SoftKeyboard;

public sealed class WordList
{
    public const string FcitxEnglish = "/usr/share/fcitx5/spell/en_dict.fscd";
    public const string HunspellDirectory = "/usr/share/hunspell";

    private static readonly byte[] FscdMagic = "FSCD0000"u8.ToArray();

    private readonly string[] _words;
    private readonly float[] _priors;

    private WordList(string[] words, float[] priors, bool weighted, string source)
    {
        _words = words;
        _priors = priors;
        Weighted = weighted;
        Source = source;
    }

    public int Count => _words.Length;

    public bool Weighted { get; }

    public string Source { get; }

    public IReadOnlyList<string> Words => _words;

    public string this[int index] => _words[index];

    public float Prior(int index) => _priors[index];

    public static WordList? ForLanguage(string language, out string? reason)
    {
        ArgumentNullException.ThrowIfNull(language);
        try
        {
            if (language == "en" && File.Exists(FcitxEnglish))
            {
                reason = null;
                return FromFscd(File.ReadAllBytes(FcitxEnglish), FcitxEnglish);
            }

            var dictionary = language switch
            {
                "en" => "en_US",
                "fr" => "fr_FR",
                "de" => "de_DE",
                "sv" => "sv_SE",
                "es" => "es_ES",
                "it" => "it_IT",
                _ => null,
            };
            if (dictionary is null)
            {
                reason = $"no word list is known for the language '{language}'";
                return null;
            }

            var aff = Path.Combine(HunspellDirectory, dictionary + ".aff");
            var dic = Path.Combine(HunspellDirectory, dictionary + ".dic");
            if (!File.Exists(aff) || !File.Exists(dic))
            {
                reason = $"no word list for '{language}': neither {FcitxEnglish} nor {dic} exists";
                return null;
            }

            reason = null;
            return FromHunspell(File.ReadAllText(aff), File.ReadAllText(dic), dic);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or FormatException)
        {
            reason = $"the word list for '{language}' cannot be read: {error.Message}";
            return null;
        }
    }

    public static WordList FromFscd(ReadOnlySpan<byte> data, string source = "fscd")
    {
        if (data.Length < 12 || !data[..8].SequenceEqual(FscdMagic))
        {
            throw new FormatException($"{source} is not an FSCD word list");
        }

        var count = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(8, 4));
        var words = new List<string>((int)Math.Min(count, 1_000_000));
        var priors = new List<float>(words.Capacity);
        var position = 12;
        for (var i = 0u; i < count && position + 2 < data.Length; i++)
        {
            var weight = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(position, 2));
            position += 2;
            var end = data[position..].IndexOf((byte)0);
            if (end < 0)
            {
                throw new FormatException($"{source} ends inside a word");
            }

            words.Add(Encoding.UTF8.GetString(data.Slice(position, end)));
            priors.Add(weight / 65535f);
            position += end + 1;
        }

        return new WordList([.. words], [.. priors], weighted: true, source);
    }

    public static WordList FromHunspell(string aff, string dic, string source = "hunspell")
    {
        ArgumentNullException.ThrowIfNull(aff);
        ArgumentNullException.ThrowIfNull(dic);
        var affix = HunspellAffixes.Parse(aff);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var words = new List<string>();
        using var reader = new StringReader(dic);
        var first = true;
        while (reader.ReadLine() is { } line)
        {
            if (first)
            {
                first = false;
                if (int.TryParse(line.Trim(), out _))
                {
                    continue;
                }
            }

            affix.Expand(line, word =>
            {
                if (seen.Add(word))
                {
                    words.Add(word);
                }
            });
        }

        var priors = new float[words.Count];
        Array.Fill(priors, 0.5f);
        return new WordList([.. words], priors, weighted: false, source);
    }
}
