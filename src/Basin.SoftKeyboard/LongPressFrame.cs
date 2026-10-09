namespace Basin.SoftKeyboard;

public readonly record struct LongPressFrame(KeyRect Frame, float PointerX, int Lines, int LineLength, int Count)
{
    public const float CellWidth = 80;
    public const float CellHeight = 60;
    public const float LeftSide = 11;
    public const float RightSide = 10;
    public const float TopToKey = 10;
    public const float PointerWidth = 25;
    public const int SingleLineMax = 5;
    public const float OneLineHeight = 90;
    public const float TwoLineHeight = 150;

    public const int Outside = -2;
    public const int Nothing = -1;

    public static LongPressFrame For(in KeyRect key, int count, float keyboardWidth)
    {
        var lines = count > SingleLineMax ? 2 : 1;
        var lineLength = (count + lines - 1) / lines;
        var height = lines > 1 ? TwoLineHeight : OneLineHeight;
        var pointer = key.X + (key.Width / 2);
        var width = LeftSide + RightSide + (lineLength * CellWidth);
        var left = pointer - (CellWidth / 2) - LeftSide;
        if (left < 0)
        {
            left = 0;
        }
        else if (left + width > keyboardWidth)
        {
            left = keyboardWidth - width;
        }

        return new LongPressFrame(new KeyRect(left, key.Y - height + TopToKey, width, height), pointer, lines, lineLength, count);
    }

    public KeyRect CellRect(int index)
    {
        var line = index / LineLength;
        var column = index % LineLength;
        return new KeyRect(Frame.X + LeftSide + (column * CellWidth), Frame.Y + TopToKey + (line * CellHeight), CellWidth, CellHeight);
    }

    public int EntryAt(float x, float y)
    {
        if (x < Frame.X || x > Frame.Right || y < Frame.Y || y > Frame.Bottom)
        {
            return Outside;
        }

        var column = Math.Min((int)((x - Frame.X - LeftSide) / CellWidth), LineLength - 1);
        var line = (int)((y - Frame.Y - TopToKey) / CellHeight);
        var index = line == 0 ? column : column + LineLength;
        return index >= 0 && index < Count ? index : Nothing;
    }
}
