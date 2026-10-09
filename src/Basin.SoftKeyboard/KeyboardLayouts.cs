using System.Globalization;
using Basin.Config;
using Basin.Diagnostics;
using Tomlyn;
using Tomlyn.Model;

namespace Basin.SoftKeyboard;

public static class KeyboardLayouts
{
    public const string Xkb = "xkb";

    private static readonly string[] BuiltIn = ["us-qwerty", "us-dvorak", "se-qwerty", "se-dvorak", "fr-azerty", "de-qwertz"];

    public static IReadOnlyList<string> Ids => BuiltIn;

    public static bool IsBuiltIn(string id) => Array.IndexOf(BuiltIn, id) >= 0;

    public static string DefaultFor(string? locale)
    {
        if (string.IsNullOrEmpty(locale))
        {
            return "us-qwerty";
        }

        if (locale.StartsWith("fr", StringComparison.OrdinalIgnoreCase))
        {
            return "fr-azerty";
        }

        if (locale.StartsWith("de", StringComparison.OrdinalIgnoreCase))
        {
            return "de-qwertz";
        }

        if (locale.StartsWith("sv", StringComparison.OrdinalIgnoreCase))
        {
            return "se-qwerty";
        }

        return "us-qwerty";
    }

    public static string LanguageLabel(string language)
    {
        if (string.IsNullOrEmpty(language))
        {
            return string.Empty;
        }

        var dash = language.IndexOfAny(['-', '_']);
        var code = dash > 0 ? language[..dash] : language;
        return char.ToUpperInvariant(code[0]) + code[1..].ToLowerInvariant();
    }

    public static KeyboardLayout? Load(string id, string? userDirectory = null, BasinLogger? log = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        var logger = log ?? SoftKeyboardLog.Log;
        if (userDirectory is not null)
        {
            var path = Path.Combine(userDirectory, id + ".toml");
            if (File.Exists(path))
            {
                try
                {
                    return Parse(File.ReadAllText(path), id, logger, path);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or TomlException or FormatException)
                {
                    logger.Warn($"{path}: {error.Message}, using the built-in layout");
                }
            }
        }

        using var stream = typeof(KeyboardLayouts).Assembly.GetManifestResourceStream("layouts/" + id + ".toml");
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd(), id, logger, "layouts/" + id + ".toml");
    }

    public static KeyboardLayout Parse(string text, string id, BasinLogger? log = null, string? source = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        var logger = log ?? SoftKeyboardLog.Log;
        var table = Toml.ToModel(text, source);
        var top = new TomlReader(table, logger);
        var name = top.Text("name") ?? id;
        var fileId = top.Text("id");
        if (fileId is not null && fileId != id)
        {
            logger.Warn($"{source}: id '{fileId}' does not match the file name, using '{id}'");
        }

        var language = top.Text("language") ?? "en";
        var units = (float)top.Number("width_units", 11.0);
        var numLock = top.Flag("num_lock", false);
        var symbolLabel = top.Text("symbol_label") ?? "+ = [  ]";
        if (top.Raw("rows") is not TomlArray rowArray || rowArray.Count != KeyGrid.Rows - 1)
        {
            throw new FormatException($"{source}: rows must be an array of {KeyGrid.Rows - 1} rows");
        }

        var rows = new KeyCell[KeyGrid.Rows - 1][];
        for (var row = 0; row < rows.Length; row++)
        {
            rows[row] = ParseRow(rowArray[row] as TomlArray, row, logger, $"{source}: rows[{row}]");
        }

        var dotCom = ParseCaps(top.Raw("dotcom") as TomlArray, logger, $"{source}: dotcom") ?? [];
        var bottom = top.Section("bottom") ?? throw new FormatException($"{source}: [bottom] is missing");
        var bottoms = new KeyCell[3][];
        bottoms[(int)BottomRow.Default] = ParseRow(bottom.Raw("default") as TomlArray, KeyGrid.Rows - 1, logger, $"{source}: bottom.default");
        bottoms[(int)BottomRow.Email] = bottom.Raw("email") is TomlArray email
            ? ParseRow(email, KeyGrid.Rows - 1, logger, $"{source}: bottom.email")
            : bottoms[(int)BottomRow.Default];
        bottoms[(int)BottomRow.Url] = bottom.Raw("url") is TomlArray url
            ? ParseRow(url, KeyGrid.Rows - 1, logger, $"{source}: bottom.url")
            : bottoms[(int)BottomRow.Default];
        top.ReportUnknown();
        return new KeyboardLayout(id, name, language, units, numLock, symbolLabel, rows, bottoms, dotCom);
    }

    private static KeyCell[] ParseRow(TomlArray? array, int row, BasinLogger log, string where)
    {
        if (array is null)
        {
            throw new FormatException($"{where}: expected an array of keys");
        }

        if (array.Count > KeyGrid.Columns)
        {
            throw new FormatException($"{where}: {array.Count} keys, at most {KeyGrid.Columns}");
        }

        var cells = new KeyCell[KeyGrid.Columns];
        Array.Fill(cells, KeyCell.Empty);
        for (var column = 0; column < array.Count; column++)
        {
            if (array[column] is not TomlTable table)
            {
                throw new FormatException($"{where}[{column}]: expected an inline table");
            }

            var reader = new TomlReader(table, log);
            var weight = (float)reader.Number("w", 1.0);
            var plainToken = reader.Text("plain");
            var plainRaw = reader.Raw("plain");
            if (plainRaw is string literal && literal.Length > 0)
            {
                plainToken = literal;
            }

            if (weight == 0 || plainToken is null)
            {
                reader.ReportUnknown();
                continue;
            }

            var label = reader.Text("label");
            if (!KeyCap.TryParse(plainToken, out var plain) || plain is null)
            {
                throw new FormatException($"{where}[{column}]: unknown key '{plainToken}'");
            }

            if (label is not null && plain.IsText)
            {
                plain = KeyCap.Character(plain.Text, label);
            }

            KeyCap? alternate = null;
            if (reader.Raw("alt") is string altToken && altToken.Length > 0)
            {
                if (!KeyCap.TryParse(altToken, out alternate))
                {
                    throw new FormatException($"{where}[{column}]: unknown alternate '{altToken}'");
                }
            }

            var longPress = ParseCaps(reader.Raw("long") as TomlArray, log, $"{where}[{column}].long");
            var keyClass = reader.Text("class") switch
            {
                "letter" => KeyClass.Letter,
                "punctuation" => KeyClass.Punctuation,
                "function" => KeyClass.Function,
                "number" => KeyClass.NumberRow,
                null => ClassOf(plain, row),
                var other => throw new FormatException($"{where}[{column}]: unknown class '{other}'"),
            };
            reader.ReportUnknown();
            cells[column] = new KeyCell(weight, plain, alternate, longPress, keyClass);
        }

        return cells;
    }

    private static KeyCap[]? ParseCaps(TomlArray? array, BasinLogger log, string where)
    {
        if (array is null)
        {
            return null;
        }

        var caps = new List<KeyCap>(array.Count);
        foreach (var item in array)
        {
            if (item is string token && KeyCap.TryParse(token, out var cap) && cap is not null)
            {
                caps.Add(cap);
            }
            else
            {
                log.Warn($"{where}: '{item}' is not a key, ignored");
            }
        }

        return [.. caps];
    }

    private static KeyClass ClassOf(KeyCap cap, int row)
    {
        if (!cap.IsText)
        {
            return KeyClass.Function;
        }

        if (row == 0)
        {
            return KeyClass.NumberRow;
        }

        return cap.IsLetter || cap.IsSpace || (cap.Text.Length == 1 && char.IsLetter(cap.Text[0]))
            ? KeyClass.Letter
            : KeyClass.Punctuation;
    }

    internal static string Format(float value) => value.ToString(CultureInfo.InvariantCulture);
}
