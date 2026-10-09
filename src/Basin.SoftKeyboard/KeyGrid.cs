namespace Basin.SoftKeyboard;

public sealed class KeyGrid
{
    public const int Rows = 5;
    public const int Columns = 14;
    public const int CellCount = Rows * Columns;

    private static readonly float[] DefaultRowHeights = [55, 70, 70, 70, 70];

    private readonly KeyCell[] _cells;
    private readonly float[] _rowHeights;

    public KeyGrid(IReadOnlyList<KeyCell> cells, IReadOnlyList<float>? rowHeights = null, float widthUnits = 11)
    {
        ArgumentNullException.ThrowIfNull(cells);
        if (cells.Count != CellCount)
        {
            throw new ArgumentException($"a key grid has {CellCount} cells, not {cells.Count}", nameof(cells));
        }

        _cells = [.. cells];
        _rowHeights = rowHeights is null ? [.. DefaultRowHeights] : [.. rowHeights];
        if (_rowHeights.Length != Rows)
        {
            throw new ArgumentException($"a key grid has {Rows} row heights", nameof(rowHeights));
        }

        WidthUnits = widthUnits;
    }

    public float WidthUnits { get; }

    public IReadOnlyList<KeyCell> Cells => _cells;

    public IReadOnlyList<float> RowHeights => _rowHeights;

    public KeyCell this[int row, int column] => _cells[(row * Columns) + column];

    public KeyCell this[int index] => _cells[index];

    public static int Index(int row, int column) => (row * Columns) + column;

    public static int RowOf(int index) => index / Columns;

    public static int ColumnOf(int index) => index % Columns;

    public float RowWeight(int row)
    {
        var total = 0f;
        for (var column = 0; column < Columns; column++)
        {
            total += MathF.Abs(this[row, column].Weight);
        }

        return total;
    }

    public void Layout(float width, float keymapHeight, Span<KeyRect> rects)
    {
        if (rects.Length < CellCount)
        {
            throw new ArgumentException($"the span needs {CellCount} rects", nameof(rects));
        }

        Span<float> rowBottoms = stackalloc float[Rows];
        Span<float> columnRights = stackalloc float[Columns];
        RowLimits(keymapHeight, rowBottoms);
        for (var row = 0; row < Rows; row++)
        {
            ColumnLimits(row, width, columnRights);
            var top = row > 0 ? rowBottoms[row - 1] : 0;
            for (var column = 0; column < Columns; column++)
            {
                var left = column > 0 ? columnRights[column - 1] : 0;
                rects[Index(row, column)] = new KeyRect(left, top, columnRights[column] - left, rowBottoms[row] - top);
            }
        }
    }

    public void RowLimits(float keymapHeight, Span<float> bottoms)
    {
        var total = 0f;
        for (var row = 0; row < Rows; row++)
        {
            total += _rowHeights[row];
        }

        var sum = 0f;
        for (var row = 0; row < Rows; row++)
        {
            sum += _rowHeights[row];
            bottoms[row] = sum * keymapHeight / total;
        }
    }

    public void ColumnLimits(int row, float width, Span<float> rights)
    {
        var total = RowWeight(row);
        var sum = 0f;
        for (var column = 0; column < Columns; column++)
        {
            sum += MathF.Abs(this[row, column].Weight);
            rights[column] = total > 0 ? sum * width / total : 0;
        }
    }

    public KeyGrid WithCell(int row, int column, KeyCell cell)
    {
        var cells = (KeyCell[])_cells.Clone();
        cells[Index(row, column)] = cell;
        return new KeyGrid(cells, _rowHeights, WidthUnits);
    }
}
