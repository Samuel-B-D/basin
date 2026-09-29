namespace Basin.Pty;

public sealed record PtyStartInfo(IReadOnlyList<string> Argv)
{
    public string? WorkingDirectory { get; init; }

    public IReadOnlyDictionary<string, string?> Environment { get; init; } = new Dictionary<string, string?>();

    public int Columns { get; init; } = 80;

    public int Rows { get; init; } = 24;

    public int PixelWidth { get; init; }

    public int PixelHeight { get; init; }
}
