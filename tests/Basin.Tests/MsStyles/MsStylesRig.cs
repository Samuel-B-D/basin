using Basin.Capabilities;
using Basin.Frames.MsStyles;
using Basin.Themes.MsStyles;

namespace Basin.Tests;

internal sealed class MsStylesRig : IDisposable
{
    public const FrameCapabilities AllThree = FrameCapabilities.WindowMenu | FrameCapabilities.Minimize | FrameCapabilities.Maximize;

    public MsStylesRig(string theme = "Plain", MsStylesLook look = MsStylesLook.Auto, string? color = null, string? size = null)
    {
        Theme = MsStylesTheme.Parse(theme == "Glassy" ? MsStylesWriter.Glassy() : MsStylesWriter.Plain(), color, size);
        Style = new MsStylesFrameStyle(Theme, look);
        Skia = new TestFrameTheme();
        Fonts = new MsStylesFonts(Skia.Typeface) { UseSystemFonts = false };
        Resources = new MsStylesResources(Style.Images) { Text = new MsStylesSkiaText(Fonts) };
    }

    public MsStylesTheme Theme { get; }

    public MsStylesFrameStyle Style { get; }

    public TestFrameTheme Skia { get; }

    public MsStylesFonts Fonts { get; }

    public MsStylesResources Resources { get; }

    public MsStylesFrameRenderer Renderer() => new(Style, Resources);

    public static FrameState StateFor(string name) => name switch
    {
        "inactive" => new FrameState { Title = "基盤 Basin", AppId = "basin", Active = false, Capabilities = AllThree },
        "maximized" => new FrameState { Title = "基盤 Basin", AppId = "basin", Active = true, Maximized = true, Capabilities = AllThree },
        "utility" => new FrameState { Title = "基盤 Basin", Active = true, Kind = FrameKind.Utility },
        "dialog" => new FrameState { Title = "基盤 Basin", Active = true, Kind = FrameKind.Dialog, Capabilities = AllThree },
        "border" => new FrameState { Title = "基盤 Basin", Active = true, Kind = FrameKind.Border },
        "no-minimize" => new FrameState { Title = "A window title far too long for the titlebar it is drawn into", AppId = "basin", Active = true, Capabilities = FrameCapabilities.Maximize },
        "plain" => new FrameState { Title = "基盤 Basin", Active = true, Capabilities = AllThree },
        "untitled" => new FrameState { Active = true, Capabilities = AllThree },
        _ => new FrameState { Title = "基盤 Basin", AppId = "basin", Active = true, Capabilities = AllThree },
    };

    public void Dispose()
    {
        Resources.Dispose();
        Fonts.Dispose();
        Skia.Dispose();
    }
}
