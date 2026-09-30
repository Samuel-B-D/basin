using Basin;
using Basin.Desktop;
using Basin.Scene;
using Basin.Shell.Xdg;
using Basin.Shell.Xdg.Protocol;

using Basin.Diagnostics;

namespace EightWm;

internal sealed partial class Shell
{
    private LayerShellSceneDriver _layerDriver = null!;
    private ShellView? _layerRelayout;
    private LayerSurface? _layerKeyboard;
    private AppWindow? _layerReturn;

    private bool LayerHasKeyboard => _layerKeyboard is { } layer && ReferenceEquals(Seat.Keyboard.Focus, layer.Surface);

    private void CreateLayerDriver() =>
        _layerDriver = new LayerShellSceneDriver(
            _services, _layout, layer => TreeFor(ViewOfOutput(layer.Output) ?? PrimaryView, layer.Layer));

    private void AttachLayerShell()
    {
        _layerDriver.TrackPopups(Shells);
        _layerDriver.SceneCreated += (layer, _) =>
        {
            var view = ViewOfOutput(layer.Output) ?? PrimaryView;
            Relayout(view);
            _report.Line($"LAYER + {layer.Namespace} {layer.Layer}");
            FocusLayer(layer);
        };
        _layerDriver.Removed += layer =>
        {
            _layerRelayout = ViewOfOutput(layer.Output) ?? PrimaryView;
            _report.Line($"LAYER - {layer.Namespace}");
            UnfocusLayer(layer);
        };
        _layerDriver.Arranged += () =>
        {
            if (_layerRelayout is { } view)
            {
                _layerRelayout = null;
                Relayout(view);
            }
        };
        _layerDriver.UsableAreaChanged += (output, usable) =>
        {
            foreach (var view in Views)
            {
                if (ReferenceEquals(view.Driver.Output, output))
                {
                    view.UsableArea = usable;
                }
            }
        };
    }

    private void FocusLayer(LayerSurface layer)
    {
        if (layer.KeyboardInteractivity == ZwlrLayerSurfaceV1.KeyboardInteractivity.None || _locked)
        {
            return;
        }

        if (Focused is { } focused)
        {
            _layerReturn = focused;
        }

        _layerKeyboard = layer;
        _router.SetKeyboardFocus(null);
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
            if (_lockedFocus is null && back is not null && _apps.Contains(back))
            {
                _lockedFocus = back;
            }

            return;
        }

        if (!ReferenceEquals(Seat.Keyboard.Focus, layer.Surface))
        {
            return;
        }

        if (back is { Surface: { } surface } && _apps.Contains(back))
        {
            Seat.Keyboard.NotifyEnter(surface);
            return;
        }

        Seat.Keyboard.NotifyClearFocus();
    }

    private void RestoreLayerKeyboard()
    {
        if (_layerKeyboard is { IsMapped: true } layer)
        {
            Seat.Keyboard.NotifyEnter(layer.Surface);
        }
    }

    private static SceneTree TreeFor(ShellView view, LayerKind kind) => kind switch
    {
        LayerKind.Background => view.Background,
        LayerKind.Bottom => view.Apps,
        LayerKind.Top => view.Chrome,
        _ => view.Overlay,
    };

    private ShellView? ViewOfOutput(OutputGlobal? global)
    {
        if (global is null)
        {
            return null;
        }

        foreach (var view in Views)
        {
            if (ReferenceEquals(view.Global, global))
            {
                return view;
            }
        }

        return null;
    }

    internal void RearrangeLayers() => _layerDriver.Rearrange();
}
