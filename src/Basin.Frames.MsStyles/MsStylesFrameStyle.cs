using Basin.Themes.MsStyles;

namespace Basin.Frames.MsStyles;

public sealed class MsStylesFrameStyle
{
    public const uint DefaultColorization = 0x45409efe;

    public const int IconSize = 16;

    private const int DefaultCaptionBar = 18;
    private const int DefaultSmallCaptionBar = 15;

    public MsStylesFrameStyle(MsStylesTheme theme, MsStylesLook look = MsStylesLook.Auto, uint colorization = DefaultColorization)
    {
        ArgumentNullException.ThrowIfNull(theme);
        Theme = theme;
        Window = theme.OpenClass("Window") ?? throw new ArgumentException("The theme has no Window class, so it cannot draw a frame.", nameof(theme));
        DwmWindow = theme.OpenClass("DWMWindow");
        Look = look switch
        {
            MsStylesLook.Auto => theme.Format == 3 ? MsStylesLook.ClassicXp : DwmWindow is not null ? MsStylesLook.Glass : MsStylesLook.Basic,
            MsStylesLook.Glass when theme.Format == 3 => throw new ArgumentException("look = glass needs a Vista or later theme (PACKTHEM 4); this one is PACKTHEM 3."),
            MsStylesLook.Glass when DwmWindow is null => throw new ArgumentException("look = glass needs the DWMWindow class, and this theme has none."),
            _ => look,
        };
        Colorization = colorization;

        var metrics = theme.SysMetrics;
        var sizing = metrics.TryGetSize(SysSize.SizingBorderWidth, out var sizingBorder) ? sizingBorder : 1;
        var padded = theme.Format >= 4 && metrics.TryGetSize(SysSize.PaddedBorderWidth, out var paddedBorder) ? paddedBorder : 0;
        Border = Math.Max(0, sizing) + Math.Max(0, padded) + 3;
        CaptionBar = metrics.TryGetSize(SysSize.CaptionBarHeight, out var captionBar) && captionBar > 0 ? captionBar : DefaultCaptionBar;
        SmallCaptionBar = metrics.TryGetSize(SysSize.SMCaptionBarHeight, out var smallBar) && smallBar > 0 ? smallBar : DefaultSmallCaptionBar;
        ButtonHeight = Math.Max(1, CaptionBar - 4);
        ButtonWidth = theme.Format >= 4
            ? VistaButtonMetrics.BasicWidth
            : Math.Max(1, (metrics.TryGetSize(SysSize.CaptionBarWidth, out var barWidth) && barWidth > 0 ? barWidth : CaptionBar) - 4);
        SmallButtonHeight = Math.Max(1, SmallCaptionBar - 4);
        SmallButtonWidth = Math.Max(1, (metrics.TryGetSize(SysSize.SMCaptionBarWidth, out var smallWidth) && smallWidth > 0 ? smallWidth : SmallCaptionBar) - 4);

        CaptionFont = metrics.TryGetFont(SysFont.CaptionFont, out var captionFont)
            ? captionFont
            : new MsStylesFont("Tahoma", 8, MsStylesFont.Bold, false, false, false);
        SmallCaptionFont = metrics.TryGetFont(SysFont.SmallCaptionFont, out var smallFont) ? smallFont : CaptionFont;
        CompositedWindow = theme.OpenClass("Window", "CompositedWindow") ?? Window;
        CaptionText = metrics.TryGetColor(SysColor.CaptionText, out var text) ? text : new MsStylesColor(255, 255, 255);
        InactiveCaptionText = metrics.TryGetColor(SysColor.InactiveCaptionText, out var inactive) ? inactive : new MsStylesColor(216, 228, 248);
    }

    public MsStylesTheme Theme { get; }

    public MsStylesLook Look { get; }

    public uint Colorization { get; }

    public MsStylesClass Window { get; }

    public MsStylesClass? DwmWindow { get; }

    public MsStylesClass CompositedWindow { get; }

    public bool IsGlass => Look == MsStylesLook.Glass;

    public float ColorizationAlpha(int part, int state)
    {
        var strength = DwmWindow is { } dwm && dwm.TryGetInt(part, state, Basin.Themes.MsStyles.ThemeProperty.ColorizationOpacity, out var opacity) ? opacity : 100;
        return (Colorization >> 24) / 255f * Math.Clamp(strength, 0, 100) / 100f;
    }

    public float PartOpacity(int part, int state)
    {
        var opacity = DwmWindow is { } dwm && dwm.TryGetInt(part, state, Basin.Themes.MsStyles.ThemeProperty.Opacity, out var value) ? value : 100;
        return Math.Clamp(opacity, 0, 100) / 100f;
    }

    public MsStylesImageCache Images { get; } = new();

    public int Border { get; }

    public int CaptionBar { get; }

    public int SmallCaptionBar { get; }

    public int ButtonWidth { get; }

    public int ButtonHeight { get; }

    public int SmallButtonWidth { get; }

    public int SmallButtonHeight { get; }

    public MsStylesFont CaptionFont { get; }

    public MsStylesFont SmallCaptionFont { get; }

    public MsStylesColor CaptionText { get; }

    public MsStylesColor InactiveCaptionText { get; }

    public int CaptionPart(bool small, bool maximized)
    {
        if (small && Window.HasPart((int)WindowPart.SmallCaption))
        {
            return (int)WindowPart.SmallCaption;
        }

        if (maximized && Window.HasPart((int)WindowPart.MaxCaption))
        {
            return (int)WindowPart.MaxCaption;
        }

        return (int)WindowPart.Caption;
    }

    public int FramePart(WindowPart normal, WindowPart small, bool isSmall) =>
        isSmall && Window.HasPart((int)small) ? (int)small : (int)normal;
}
