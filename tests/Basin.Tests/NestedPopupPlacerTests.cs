using Basin.Desktop;
using Basin.Scene;
using Basin.Shell.Xdg;
using Basin.Shell.Xdg.Protocol;
using Xunit;

namespace Basin.Tests;

public sealed class NestedPopupPlacerTests
{
    [Fact]
    public void A_popup_nests_under_its_parent_is_constrained_to_the_output_and_forgotten_when_destroyed()
    {
        using var host = new CompositorTestHost();
        var placer = new NestedPopupPlacer(host.Layout);
        var width = host.Layout.BoxOf(host.Output).Width;
        var changed = 0;
        placer.Changed += () => changed++;

        var popups = new List<XdgPopupWindow>();
        var placed = new List<SceneSurface?>();
        host.Shell.NewPopup += popup =>
        {
            popups.Add(popup);
            placed.Add(placer.Attach(popup));
        };

        var client = host.Client;
        var parent = MappedToplevel.Map(host, client);
        var toplevelScene = new SceneSurface(host.Scene.Root, parent.ServerSurface);
        toplevelScene.Tree.SetPosition(width - 60, 100);
        placer.Add(parent.ServerToplevel.Xdg, toplevelScene);

        var (first, firstXdg, firstProxy) = OpenPopup(client, parent.XdgSurface);
        host.PumpUntil(() => popups.Count == 1);
        Commit(client, first);
        host.PumpToServer();

        var menu = placed[0]!;
        Assert.Same(toplevelScene.Tree, menu.Tree.Parent);
        var position = popups[0].SurfacePosition;
        var geometry = parent.ServerToplevel.Xdg.EffectiveGeometry;
        Assert.Equal(geometry.X + position.X, menu.Tree.X);
        Assert.True(width - 60 + menu.Tree.X + 30 <= width, "the popup stays on the output");
        Assert.True(placer.TryGet(popups[0].Xdg, out _));

        var (second, secondXdg, secondProxy) = OpenPopup(client, firstXdg);
        host.PumpUntil(() => popups.Count == 2);
        Commit(client, second);
        host.PumpToServer();
        Assert.Same(menu.Tree, placed[1]!.Tree.Parent);
        Assert.Same(parent.ServerToplevel, NestedPopupPlacer.RootOf(popups[1]));

        var before = changed;
        secondProxy.Destroy();
        secondXdg.Destroy();
        second.Dispose();
        host.PumpToServer();
        Assert.False(placer.TryGet(popups[1].Xdg, out _));
        Assert.True(changed > before);

        firstProxy.Destroy();
        firstXdg.Destroy();
        first.Dispose();
        host.PumpToServer();
        Assert.False(placer.TryGet(popups[0].Xdg, out _));
        toplevelScene.Destroy();
    }

    private static (Wayland.WlSurface Surface, XdgSurface Xdg, XdgPopup Popup) OpenPopup(ShmTestClient client, XdgSurface parent)
    {
        var positioner = client.WmBase!.CreatePositioner();
        positioner.SetSize(30, 30);
        positioner.SetAnchorRect(40, 40, 1, 1);
        positioner.SetAnchor(XdgPositioner.Anchor.BottomRight);
        positioner.SetGravity(XdgPositioner.Gravity.BottomRight);
        positioner.SetConstraintAdjustment(XdgPositioner.ConstraintAdjustment.SlideX);
        var surface = client.Compositor.CreateSurface();
        var xdg = client.WmBase.GetXdgSurface(surface);
        var popup = xdg.GetPopup(parent, positioner);
        xdg.Configure += (_, e) => xdg.AckConfigure(e.Serial);
        surface.Commit();
        return (surface, xdg, popup);
    }

    private static void Commit(ShmTestClient client, Wayland.WlSurface surface)
    {
        var buffer = client.CreateBuffer(30, 30, Fill.Solid(30, 30, 0xFF884422));
        surface.Attach(buffer.Proxy, 0, 0);
        surface.Commit();
    }
}
