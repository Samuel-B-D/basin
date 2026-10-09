namespace Basin.SoftKeyboard.Fcitx5;

public sealed record Fcitx5Candidates(IReadOnlyList<string> Words, bool HasPrevious, bool HasNext, int Page, int Cursor)
{
    public static readonly Fcitx5Candidates Empty = new([], false, false, -1, -1);
}
