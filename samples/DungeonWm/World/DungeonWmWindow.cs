using Basin;
using Basin.Scene;
using Basin.Scene3D;
using Basin.Shell.Xdg;
using Basin.XWayland;

namespace DungeonWm;

internal sealed class DungeonWmWindow
{
    public DungeonWmWindow(XdgToplevelWindow toplevel, SceneSurface scene)
    {
        Toplevel = toplevel;
        Handle = toplevel;
        Scene = scene;
        World = new WorldWindow { Tag = this };
    }

    public DungeonWmWindow(XWaylandWindow x, SceneSurface scene)
    {
        X = x;
        Handle = x;
        Scene = scene;
        World = new WorldWindow { Tag = this };
    }

    public XdgToplevelWindow? Toplevel { get; }

    public XWaylandWindow? X { get; }

    public Basin.Capabilities.IToplevelHandle Handle { get; }

    public SceneSurface Scene { get; }

    public WorldWindow World { get; }

    public Surface Surface => Scene.Surface;

    public DungeonWmWindowState State { get; set; }

    public MapRegion? Room { get; set; }

    public WallSlot? Slot { get; set; }

    public bool Wide { get; set; }

    public DungeonWmFrameStyle? FrameStyle { get; set; }

    public PlaqueButton PlaqueHover { get; set; }

    public PlaqueButton PlaquePressed { get; set; }

    public float Fade { get; set; } = 1;

    public float CanvasFade { get; set; } = 1;

    public float Hanging { get; set; } = -1;

    public int Swing { get; set; } = 1;

    public ulong ToplevelId { get; set; }

    public string AppId => Handle.AppId ?? string.Empty;

    public string Title => Handle.Title ?? string.Empty;

    public int MinWidth => Toplevel?.MinWidth ?? X?.MinWidth ?? 0;

    public int MinHeight => Toplevel?.MinHeight ?? X?.MinHeight ?? 0;

    public int MaxWidth => Toplevel?.MaxWidth ?? X?.MaxWidth ?? 0;

    public int MaxHeight => Toplevel?.MaxHeight ?? X?.MaxHeight ?? 0;

    public Box Geometry => Toplevel is { } toplevel
        ? toplevel.Xdg.EffectiveGeometry
        : new Box(0, 0, X?.Width ?? 1, X?.Height ?? 1);

    public void SetActivated(bool activated) => Handle.SetActivated(activated);

    public void SetResizing(bool resizing) => Toplevel?.SetResizing(resizing);

    public void RequestSize(int width, int height)
    {
        if (Toplevel is { } toplevel)
        {
            toplevel.SetSize(width, height);
            toplevel.RequestConfigure();
        }
        else if (X is { } x && width > 0 && height > 0)
        {
            x.Configure(x.X, x.Y, width, height);
        }
    }

    public void SetFullscreen(bool fullscreen, Box area)
    {
        if (Toplevel is { } toplevel)
        {
            toplevel.SetFullscreen(fullscreen);
            if (!fullscreen)
            {
                toplevel.SetMaximized(false);
            }

            toplevel.SetSize(fullscreen ? area.Width : 0, fullscreen ? area.Height : 0);
            toplevel.RequestConfigure();
        }
        else if (X is { } x)
        {
            x.SetFullscreen(fullscreen);
            if (fullscreen)
            {
                _restore = _restore.Saving(new Box(x.X, x.Y, x.Width, x.Height));
                x.Configure(area.X, area.Y, area.Width, area.Height);
            }
            else if (_restore.TryGet(out var restore))
            {
                x.Configure(restore.X, restore.Y, restore.Width, restore.Height);
                _restore = RestoreGeometry.None;
            }
        }
    }

    public void Close() => Handle.Close();

    private RestoreGeometry _restore;
}
