using Basin.Shell.Xdg;

namespace Basin.Avalonia;

public sealed class ToplevelLayerShellDriver : ILayerShellDriver
{
    private readonly List<LayerSurface> _pending = [];
    private LayerShell? _shell;
    private Action<LayerSurface>? _target;

    public ToplevelLayerShellDriver(BasinServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Use<ILayerShellDriver>(this);
    }

    void ILayerShellDriver.Attach(LayerShell shell)
    {
        ArgumentNullException.ThrowIfNull(shell);
        if (_shell is not null)
        {
            throw new InvalidOperationException("this layer-shell driver already drives a layer shell");
        }

        _shell = shell;
        shell.NewSurface += OnNewSurface;
    }

    internal void Bind(Action<LayerSurface> target)
    {
        if (_target is not null)
        {
            throw new InvalidOperationException("this layer-shell driver already drives a ToplevelWindows");
        }

        _target = target;
        var pending = _pending.ToArray();
        _pending.Clear();
        foreach (var layer in pending)
        {
            target(layer);
        }
    }

    internal void Unbind(Action<LayerSurface> target)
    {
        if (_target == target)
        {
            _target = null;
        }
    }

    private void OnNewSurface(LayerSurface layer)
    {
        if (_target is { } target)
        {
            target(layer);
            return;
        }

        _pending.Add(layer);
        layer.Destroyed += () => _pending.Remove(layer);
    }
}
