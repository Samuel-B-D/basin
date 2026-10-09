using Basin;
using Basin.Desktop;
using Basin.Scene;
using Basin.Shell.Xdg;
using Basin.Shell.Xdg.Protocol;

namespace LunaWm;

internal sealed partial class LunaShell
{
    private LayerShellSceneDriver _layerDriver = null!;
    private SessionLockSceneDriver _lockDriver = null!;
    private LayerSurface? _layerKeyboard;
    private Card? _layerReturn;
    private bool _sessionLocked;

    private void CreateLayerDrivers()
    {
        _layerDriver = new LayerShellSceneDriver(_services, _layout, layer => LayerTree(ViewOfOutput(layer.Output) ?? PrimaryView, layer.Layer));
        _lockDriver = new SessionLockSceneDriver(_services, _sessionLockLayer, _layout, SetSessionLocked);
    }

    private void AttachLayers()
    {
        _layerDriver.Accept = _ => _views.Count > 0;
        _layerDriver.DefaultOutput = _ => _views.Count > 0 ? PlacementView().Global : null;
        _layerDriver.TrackPopups(Shells);
        _layerDriver.SceneCreated += (layer, _) =>
        {
            _report.Line($"LAYER + {layer.Namespace} {layer.Layer}");
            FocusLayer(layer);
        };
        _layerDriver.Removed += layer =>
        {
            _report.Line($"LAYER - {layer.Namespace}");
            UnfocusLayer(layer);
        };
        _layerDriver.UsableAreaChanged += (output, usable) =>
        {
            if (ViewOfOutput(output) is { } view)
            {
                view.UsableArea = usable;
            }
        };
        _layerDriver.Arranged += _outputs.ScheduleAll;

        _lockDriver.Locked += () => _report.Line("LOCKED session");
        _lockDriver.Unlocked += () => _report.Line("UNLOCKED session");
        _lockDriver.Abandoned += () => _report.Line("LOCK ABANDONED (staying blanked)");
    }

    private static SceneTree LayerTree(LunaView view, LayerKind kind) => kind switch
    {
        LayerKind.Background => view.LayerBackground,
        LayerKind.Bottom => view.LayerBottom,
        LayerKind.Top => view.LayerTop,
        _ => view.LayerOverlay,
    };

    private LunaView? ViewOfOutput(IOutput? output)
    {
        foreach (var view in _views)
        {
            if (ReferenceEquals(view.Output, output))
            {
                return view;
            }
        }

        return null;
    }

    private LunaView? ViewOfOutput(OutputGlobal? global)
    {
        foreach (var view in _views)
        {
            if (ReferenceEquals(view.Global, global))
            {
                return view;
            }
        }

        return null;
    }

    private void RelayoutLayers()
    {
        _layerDriver.Rearrange();
        _lockDriver.Reconfigure();
    }

    private void FocusLayer(LayerSurface layer)
    {
        if (layer.KeyboardInteractivity == ZwlrLayerSurfaceV1.KeyboardInteractivity.None || _locked)
        {
            return;
        }

        _layerReturn = Focused;
        _layerKeyboard = layer;
        _seat.ClearChromeFocus();
        Seat.Keyboard.NotifyEnter(layer.Surface);
    }

    private void UnfocusLayer(LayerSurface layer)
    {
        if (!ReferenceEquals(_layerKeyboard, layer))
        {
            return;
        }

        _layerKeyboard = null;
        var back = _layerReturn;
        _layerReturn = null;
        if (_locked)
        {
            return;
        }

        if (back is { Mapped: true })
        {
            Focus(back);
            return;
        }

        Seat.Keyboard.NotifyClearFocus();
    }

    private void SetSessionLocked(bool locked)
    {
        _sessionLocked = locked;
        ApplyLockState();
    }

    private void RestoreKeyboard()
    {
        if (_layerKeyboard is { IsMapped: true } layer)
        {
            Seat.Keyboard.NotifyEnter(layer.Surface);
            return;
        }

        if (_lockedFocus is { Mapped: true } card)
        {
            _lockedFocus = null;
            Focus(card);
            return;
        }

        _lockedFocus = null;
        foreach (var view in _views)
        {
            if (!view.InCardView && view.Deck.ActiveCard is { } active)
            {
                Focus(active);
                return;
            }
        }

        Seat.Keyboard.NotifyClearFocus();
    }
}
