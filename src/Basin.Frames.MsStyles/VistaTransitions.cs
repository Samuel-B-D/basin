namespace Basin.Frames.MsStyles;

internal static class VistaTransitions
{
    public const long HoverInNanos = 80_000_000;
    public const long HoverOutNanos = 165_000_000;

    public static float FadeIn(long elapsed) => elapsed >= HoverInNanos ? 1f : Math.Max(0f, (float)elapsed / HoverInNanos);

    public static float FadeOut(long elapsed) => elapsed >= HoverOutNanos ? 0f : 1f - Math.Max(0f, (float)elapsed / HoverOutNanos);
}
