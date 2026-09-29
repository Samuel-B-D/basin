using Basin.Desktop;
using Basin.Scene;
using Basin.Shell.Xdg;
using Wayland;
using Xunit;

namespace Basin.Tests;

public sealed class RotatedInputTests
{
    private const double Degrees30 = Math.PI / 6;

    private static RenderTransform Turned(double x, double y, int width, int height) =>
        RenderTransform.Multiply(
            RenderTransform.Translation(x, y),
            RenderTransform.RotationAbout(Degrees30, width / 2.0, height / 2.0));

    private static (double X, double Y) Local(in RenderTransform matrix, double x, double y)
    {
        Assert.True(matrix.TryInvert(out var inverse));
        return inverse.Map(x, y);
    }

    private static SceneTransform Rotate(CompositorTestHost host, Surface surface, in RenderTransform matrix)
    {
        foreach (var hosted in host.SurfaceScenes)
        {
            if (ReferenceEquals(hosted.Surface, surface))
            {
                hosted.Tree.Enabled = false;
            }
        }

        var frame = new SceneTransform(host.Scene.Root) { Matrix = matrix };
        _ = new SceneSurface(frame, surface);
        return frame;
    }

    private static void Point(SceneSeatInput input, uint timeMs, double x, double y)
    {
        input.Pointer.Warp(x, y);
        input.PointerMotion(timeMs, 0, 0);
    }

    [Fact]
    public void Enter_and_motion_through_a_rotated_window_are_surface_local()
    {
        using var host = new CompositorTestHost(400, 300);
        var client = host.Client;
        var window = MappedToplevel.Map(host, client, 80, 60);
        var matrix = Turned(150, 100, 80, 60);
        Rotate(host, window.ServerSurface, matrix);
        var input = new SceneSeatInput(host.Seat, host.Scene, host.Layout);

        var pointer = client.Seat!.GetPointer();
        (double X, double Y)? entered = null;
        var motions = new List<(double X, double Y)>();
        pointer.Enter += (_, e) => entered = (e.SurfaceX.ToDouble(), e.SurfaceY.ToDouble());
        pointer.Motion += (_, e) => motions.Add((e.SurfaceX.ToDouble(), e.SurfaceY.ToDouble()));
        host.PumpToClient();

        Point(input, 1, 190, 130);
        Point(input, 2, 180, 140);
        host.PumpUntil(() => entered is not null && motions.Count == 2);

        var expected = Local(matrix, 190, 130);
        Assert.Equal(expected.X, entered!.Value.X, 0.01);
        Assert.Equal(expected.Y, entered.Value.Y, 0.01);
        var moved = Local(matrix, 180, 140);
        Assert.Equal(moved.X, motions[1].X, 0.01);
        Assert.Equal(moved.Y, motions[1].Y, 0.01);
    }

    [Fact]
    public void An_implicit_grab_keeps_mapping_through_the_rotation()
    {
        using var host = new CompositorTestHost(400, 300);
        var client = host.Client;
        var window = MappedToplevel.Map(host, client, 80, 60);
        var matrix = Turned(150, 100, 80, 60);
        Rotate(host, window.ServerSurface, matrix);
        var input = new SceneSeatInput(host.Seat, host.Scene, host.Layout);

        var pointer = client.Seat!.GetPointer();
        var motions = new List<(double X, double Y)>();
        pointer.Motion += (_, e) => motions.Add((e.SurfaceX.ToDouble(), e.SurfaceY.ToDouble()));
        host.PumpToClient();

        Point(input, 1, 190, 130);
        input.PointerButton(2, 0x110, pressed: true);
        Point(input, 3, 320, 40);
        input.PointerButton(4, 0x110, pressed: false);
        host.PumpUntil(() => motions.Count == 2);

        var expected = Local(matrix, 320, 40);
        Assert.Equal(expected.X, motions[1].X, 0.01);
        Assert.Equal(expected.Y, motions[1].Y, 0.01);
    }

    [Fact]
    public void A_touch_that_went_down_on_a_rotated_window_moves_in_its_coordinates()
    {
        var scene = new Scene.Scene();
        var matrix = Turned(150, 100, 80, 60);
        var frame = new SceneTransform(scene.Root) { Matrix = matrix };
        var rect = new SceneRect(frame, 80, 60, new RenderColor(1f, 1f, 1f, 1f));

        var touch = new TouchPoints();
        touch.Down(1, 190, 130, rect);

        Assert.True(touch.TryMotion(1, 330, 20, out var localX, out var localY));
        var expected = Local(matrix, 330, 20);
        Assert.Equal(expected.X, localX, 6);
        Assert.Equal(expected.Y, localY, 6);
    }

    [Fact]
    public void A_drag_over_a_rotated_target_enters_at_its_local_coordinates()
    {
        using var host = new CompositorTestHost(400, 300);
        var clientA = host.Client;
        var clientB = host.ConnectClient();
        var windowA = MappedToplevel.Map(host, clientA);
        var windowB = MappedToplevel.Map(host, clientB, 80, 60);
        var matrix = Turned(250, 150, 80, 60);
        Rotate(host, windowB.ServerSurface, matrix);
        var input = new SceneSeatInput(host.Seat, host.Scene, host.Layout);

        var deviceA = clientA.DataDeviceManager!.GetDataDevice(clientA.Seat!);
        var deviceB = clientB.DataDeviceManager!.GetDataDevice(clientB.Seat!);
        var pointerA = clientA.Seat!.GetPointer();
        uint pressSerial = 0;
        pointerA.Button += (_, e) =>
        {
            if (e.State == WlPointer.ButtonState.Pressed)
            {
                pressSerial = e.Serial;
            }
        };
        host.PumpToClient();
        Point(input, 1, 10, 10);
        input.PointerButton(2, 0x110, pressed: true);
        host.PumpUntil(() => pressSerial != 0);

        var source = clientA.DataDeviceManager.CreateDataSource();
        source.Offer("text/plain");
        source.SetActions(WlDataDeviceManager.DndAction.Copy);
        deviceA.StartDrag(source, windowA.Surface, null, pressSerial);
        host.PumpToServer();

        (double X, double Y)? entered = null;
        var motions = new List<(double X, double Y)>();
        deviceB.DataOffer += (_, e) => e.Id.Offer += (_, _) => { };
        deviceB.Enter += (_, e) => entered = (e.X.ToDouble(), e.Y.ToDouble());
        deviceB.Motion += (_, e) => motions.Add((e.X.ToDouble(), e.Y.ToDouble()));
        host.PumpToClient();

        Point(input, 3, 290, 180);
        Point(input, 4, 285, 190);
        host.PumpUntil(() => entered is not null && motions.Count >= 1);

        var expected = Local(matrix, 290, 180);
        Assert.Equal(expected.X, entered!.Value.X, 0.01);
        Assert.Equal(expected.Y, entered.Value.Y, 0.01);
        var moved = Local(matrix, 285, 190);
        Assert.Equal(moved.X, motions[^1].X, 0.01);
        Assert.Equal(moved.Y, motions[^1].Y, 0.01);

        input.PointerButton(5, 0x110, pressed: false);
        host.PumpToClient();
    }

    [Fact]
    public void A_popup_follows_its_parents_rotation()
    {
        using var host = new CompositorTestHost(400, 300);
        var placer = new PopupPlacer(host.Layout);
        var parentTree = new SceneTree(host.Scene.Root);
        var origin = new Point(150, 100);
        var turn = RenderTransform.RotationAbout(Degrees30, 40, 30);

        XdgPopupWindow? serverPopup = null;
        SceneSurface? placed = null;
        host.Shell.NewPopup += popup =>
        {
            serverPopup = popup;
            placed = placer.Attach(popup, parentTree, origin: () => origin, transform: () => turn);
        };

        var client = host.Client;
        var parent = MappedToplevel.Map(host, client, 80, 60);
        var positioner = client.WmBase!.CreatePositioner();
        positioner.SetSize(30, 30);
        positioner.SetAnchorRect(20, 20, 1, 1);
        var popupSurface = client.Compositor.CreateSurface();
        var popupXdg = client.WmBase.GetXdgSurface(popupSurface);
        _ = popupXdg.GetPopup(parent.XdgSurface, positioner);
        popupXdg.Configure += (_, e) => popupXdg.AckConfigure(e.Serial);
        popupSurface.Commit();
        host.PumpUntil(() => serverPopup is not null);

        var buffer = client.CreateBuffer(30, 30, Fill.Solid(30, 30, 0xFF884422));
        popupSurface.Attach(buffer.Proxy, 0, 0);
        popupSurface.Commit();
        host.PumpToServer();
        foreach (var hosted in host.SurfaceScenes)
        {
            if (ReferenceEquals(hosted.Surface, serverPopup!.Surface) && !ReferenceEquals(hosted, placed))
            {
                hosted.Tree.Enabled = false;
            }
        }

        var position = serverPopup!.SurfacePosition;
        var (layoutX, layoutY) = turn.Map(position.X + 5, position.Y + 7);
        Assert.True(host.Scene.TryMapToSurface(serverPopup.Surface, origin.X + layoutX, origin.Y + layoutY, out var x, out var y));
        Assert.Equal(5, x, 6);
        Assert.Equal(7, y, 6);

        turn = RenderTransform.Identity;
        placer.PlaceAll();
        Assert.True(host.Scene.TryMapToSurface(serverPopup.Surface, origin.X + position.X + 5, origin.Y + position.Y + 7, out x, out y));
        Assert.Equal(5, x, 6);
        Assert.Equal(7, y, 6);
    }
}
