namespace Basin.Themes.MsStyles;

public sealed record MsStylesDocumentation
{
    public string? Name { get; init; }

    public string? DisplayName { get; init; }

    public string? Tooltip { get; init; }

    public string? Author { get; init; }

    public string? Company { get; init; }

    public string? Copyright { get; init; }

    public string? Url { get; init; }

    public string? Version { get; init; }

    public string? Description { get; init; }
}
