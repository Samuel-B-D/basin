namespace Basin.SoftKeyboard;

public readonly record struct TypedKey(KeyCap Cap, bool Capital, bool ShiftHeld, bool Repeat, bool WordDelete)
{
    public string Text => Capital ? Cap.UpperText : Cap.Text;
}
