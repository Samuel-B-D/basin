namespace Basin.SoftKeyboard;

public sealed class KeyboardLayout
{
    public const int SymbolColumn = 1;

    private readonly KeyCell[][] _rows;
    private readonly KeyCell[][] _bottoms;

    internal KeyboardLayout(
        string id, string name, string language, float widthUnits, bool numLock, string symbolLabel,
        KeyCell[][] rows, KeyCell[][] bottoms, IReadOnlyList<KeyCap> dotCom)
    {
        Id = id;
        Name = name;
        Language = language;
        WidthUnits = widthUnits;
        NumLock = numLock;
        SymbolLabel = symbolLabel;
        _rows = rows;
        _bottoms = bottoms;
        DotCom = dotCom;
    }

    public const string LockedSymbolLabel = "A B C";

    public string Id { get; }

    public string Name { get; }

    public string Language { get; }

    public float WidthUnits { get; }

    public bool NumLock { get; }

    public string SymbolLabel { get; }

    public IReadOnlyList<KeyCap> DotCom { get; }

    public string LanguageLabel => LabelOverride ?? KeyboardLayouts.LanguageLabel(Language);

    internal string? LabelOverride { get; init; }

    public KeyGrid Grid(BottomRow bottom = BottomRow.Default, IReadOnlyList<KeyCap>? languages = null, string? languageLabel = null)
    {
        var cells = new KeyCell[KeyGrid.CellCount];
        for (var row = 0; row < KeyGrid.Rows - 1; row++)
        {
            Array.Copy(_rows[row], 0, cells, row * KeyGrid.Columns, KeyGrid.Columns);
        }

        var last = _bottoms[(int)bottom];
        Array.Copy(last, 0, cells, (KeyGrid.Rows - 1) * KeyGrid.Columns, KeyGrid.Columns);
        if (languages is { Count: >= 2 })
        {
            var symbolIndex = KeyGrid.Index(KeyGrid.Rows - 1, SymbolColumn);
            var symbol = cells[symbolIndex];
            if (symbol.Plain?.Function == KeyFunction.Symbol && symbol.Weight >= 2)
            {
                cells[symbolIndex] = symbol.WithWeight(symbol.Weight - 1);
                var cap = KeyCap.LanguageKey(languageLabel ?? LanguageLabel);
                cells[symbolIndex + 1] = new KeyCell(1, cap, cap, languages, KeyClass.Function);
            }
        }

        return new KeyGrid(cells, null, WidthUnits);
    }

    internal KeyCell[] Row(int row) => row < KeyGrid.Rows - 1 ? _rows[row] : _bottoms[0];

    internal KeyCell[] Bottom(BottomRow bottom) => _bottoms[(int)bottom];
}
