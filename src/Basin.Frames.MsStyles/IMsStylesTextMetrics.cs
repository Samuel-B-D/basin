using Basin.Themes.MsStyles;

namespace Basin.Frames.MsStyles;

public interface IMsStylesTextMetrics
{
    float Measure(string text, in MsStylesFont font, float pixels);
}
