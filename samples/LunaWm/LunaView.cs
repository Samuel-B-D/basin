using Basin;
using Basin.Host;
using Basin.Scene;

namespace LunaWm;

internal sealed class LunaView
{
    public const int StatusBarHeight = 28;

    public LunaView(OutputView driver, Scene scene)
    {
        Driver = driver;
        Root = new SceneTree(scene.Root);
        Background = new SceneTree(Root);
        Backdrop = new SceneRect(Background, 1, 1, LunaColors.Backdrop);
        LayerBackground = new SceneTree(Background);
        LayerBottom = new SceneTree(Background);
        Cards = new SceneTree(Root);
        Launcher = new SceneTree(Root);
        QuickLaunch = new SceneTree(Root);
        Dashboard = new SceneTree(Root);
        StatusBar = new SceneTree(Root);
        StatusBarFrame = new SceneTransform(StatusBar);
        Menus = new SceneTree(Root);
        LayerTop = new SceneTree(Root);
        LayerOverlay = new SceneTree(Root);
        Lock = new SceneTree(Root);
        InputMethod = new SceneTree(Root);
        Reposition();
    }

    public OutputView Driver { get; }

    public IOutput Output => Driver.Output;

    public OutputGlobal Global => Driver.Global;

    public SceneTree Root { get; }

    public SceneTree Background { get; }

    public SceneRect Backdrop { get; }

    public SceneBuffer? Wallpaper { get; set; }

    public SceneTree LayerBackground { get; }

    public SceneTree LayerBottom { get; }

    public SceneTree Cards { get; }

    public SceneTree Launcher { get; }

    public SceneTree QuickLaunch { get; }

    public SceneTree Dashboard { get; }

    public SceneTree StatusBar { get; }

    public SceneTransform StatusBarFrame { get; }

    public SceneTree Menus { get; }

    public SceneTree LayerTop { get; }

    public SceneTree LayerOverlay { get; }

    public SceneTree Lock { get; }

    public SceneTree InputMethod { get; }

    public CardDeck<Card> Deck { get; } = new();

    public bool InCardView { get; set; } = true;

    public CardMetrics Metrics { get; set; }

    public double Scroll { get; set; }

    public SceneTransform? Ghost { get; set; }

    public SceneRect? GhostFill { get; set; }

    public CardShadow? GhostShadow { get; set; }

    public Basin.MemoryBuffer? GhostIcon { get; set; }

    public Motion GhostPulse;

    public long GhostStarted { get; set; }

    public bool CardsSettled { get; set; } = true;

    public OutputScheduler? Scheduler => Driver.Scheduler;

    public StatusBar? Bar { get; set; }

    public MenuSurface? Menu { get; set; }

    public LauncherSurface? LauncherUi { get; set; }

    public DockSurface? Dock { get; set; }

    public DashboardSurface? DashboardUi { get; set; }

    public AlertSurface? Alert { get; set; }

    public LockSurface? LockUi { get; set; }

    public JustTypeSurface? JustType { get; set; }

    public KeyboardSurface? Keyboard { get; set; }

    public int CardInset { get; set; }

    public Box UsableArea { get; set; }

    public Box Box => Driver.Box;

    public int Width => Driver.Width;

    public int Height => Driver.Height;

    public double Scale => Driver.Scale;

    public bool BarHidden { get; set; }

    public bool BarRevealed { get; set; }

    public int TopInset => BarHidden && !BarRevealed ? 0 : StatusBarHeight;

    public Box AppBox => new(0, StatusBarHeight, Box.Width, Math.Max(1, Box.Height - StatusBarHeight));

    public Box FullBox => new(0, 0, Box.Width, Box.Height);

    public void Reposition()
    {
        Root.SetPosition(Driver.Box.X, Driver.Box.Y);
        Backdrop.Width = Math.Max(1, Driver.Box.Width);
        Backdrop.Height = Math.Max(1, Driver.Box.Height);
        Cards.ClipBox = new Box(0, 0, Math.Max(1, Driver.Box.Width), Math.Max(1, Driver.Box.Height));
    }

    public void ReleaseChrome()
    {
        Bar?.Dispose();
        Bar = null;
        Menu?.Dispose();
        Menu = null;
        LauncherUi?.Dispose();
        LauncherUi = null;
        Dock?.Dispose();
        Dock = null;
        DashboardUi?.Dispose();
        DashboardUi = null;
        Alert?.Dispose();
        Alert = null;
        LockUi?.Dispose();
        LockUi = null;
        JustType?.Dispose();
        JustType = null;
        Keyboard?.Dispose();
        Keyboard = null;
    }

    public void Destroy()
    {
        ReleaseChrome();
        if (!Root.IsDestroyed)
        {
            Root.Destroy();
        }
    }
}
