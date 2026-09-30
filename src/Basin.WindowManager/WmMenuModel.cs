namespace Basin.WindowManager;

public sealed class WmMenuModel
{
    private readonly int[] _heights;
    private readonly bool[] _selectable;
    private (int Scale, int Hovered)? _rendered;

    public WmMenuModel(int rows)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rows);
        _heights = new int[rows];
        _selectable = new bool[rows];
        Array.Fill(_selectable, true);
    }

    public int Count => _heights.Length;

    public Rect Content { get; set; }

    public int Hovered { get; private set; } = -1;

    public int Height(int row) => _heights[row];

    public void SetRow(int row, int height, bool selectable = true)
    {
        _heights[row] = Math.Max(height, 0);
        _selectable[row] = selectable;
    }

    public void SetSelectable(int row, bool selectable) => _selectable[row] = selectable;

    public bool IsSelectable(int row) => row >= 0 && row < Count && _selectable[row];

    public int RowsHeight()
    {
        var total = 0;
        foreach (var height in _heights)
        {
            total += height;
        }

        return total;
    }

    public int? RowAt(int x, int y)
    {
        if (x < Content.X || x >= Content.X + Content.Width || y < Content.Y)
        {
            return null;
        }

        var offset = Content.Y;
        for (var i = 0; i < _heights.Length; i++)
        {
            if (y < offset + _heights[i])
            {
                return i;
            }

            offset += _heights[i];
        }

        return null;
    }

    public int? SelectableAt(int x, int y) => RowAt(x, y) is { } row && _selectable[row] ? row : null;

    public Rect RowRect(int row)
    {
        var offset = Content.Y;
        for (var i = 0; i < row; i++)
        {
            offset += _heights[i];
        }

        return new Rect(Content.X, offset, Content.Width, _heights[row]);
    }

    public bool Hover(int x, int y) => SetHovered(SelectableAt(x, y) ?? -1);

    public bool ClearHover() => SetHovered(-1);

    public bool SetHovered(int row)
    {
        if (row == Hovered)
        {
            return false;
        }

        Hovered = row;
        return true;
    }

    public bool Step(int delta)
    {
        if (Count == 0)
        {
            return false;
        }

        for (var step = 1; step <= Count; step++)
        {
            var row = ((Hovered + (delta * step)) % Count + Count) % Count;
            if (_selectable[row])
            {
                return SetHovered(row);
            }
        }

        return false;
    }

    public bool SelectFirst()
    {
        for (var row = 0; row < Count; row++)
        {
            if (_selectable[row])
            {
                return SetHovered(row);
            }
        }

        return SetHovered(-1);
    }

    public bool NeedsRender(int scale) => _rendered != (scale, Hovered);

    public void Rendered(int scale) => _rendered = (scale, Hovered);

    public void Invalidate() => _rendered = null;

    public static Point ClampInto(Point origin, Size size, Size area) => new(
        Math.Clamp(origin.X, 0, Math.Max(area.Width - size.Width, 0)),
        Math.Clamp(origin.Y, 0, Math.Max(area.Height - size.Height, 0)));
}
