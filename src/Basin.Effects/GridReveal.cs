namespace Basin.Effects;

public readonly record struct GridReveal(GridRevealStyle Style, float Progress, double OriginX, double OriginY, double Feather)
{
    public static GridReveal Full { get; } = new(GridRevealStyle.None, 1f, 0.0, 0.0, 0.0);

    public bool IsFull => Progress >= 1f;
}
