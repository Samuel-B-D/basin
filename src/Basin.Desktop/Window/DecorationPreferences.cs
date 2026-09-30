using Basin.Shell.Xdg;

namespace Basin.Desktop;

public sealed class DecorationPreferences
{
    private readonly Dictionary<Surface, bool> _serverSide = [];
    private readonly XdgDecorationManager? _xdg;
    private readonly KdeServerDecorationManager? _kde;

    public DecorationPreferences(
        XdgDecorationManager? xdg,
        KdeServerDecorationManager? kde = null,
        XdgToplevelSource? toplevels = null)
    {
        _xdg = xdg;
        _kde = kde;
        if (xdg is not null)
        {
            xdg.ModeChanged += (toplevel, mode) => Record(toplevel.Surface, mode == DecorationMode.ServerSide);
        }

        if (kde is not null)
        {
            kde.ModeRequested += (surface, mode) =>
                Record(surface, mode == KdeServerDecorationManager.DecorationMode.Server);
        }

        if (toplevels is not null)
        {
            toplevels.NoBorderRequested += (toplevel, noBorder) => Record(toplevel.Surface, !noBorder);
        }
    }

    public event Action<Surface, bool>? Changed;

    public void Record(Surface surface, bool serverSide)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if (_serverSide.TryAdd(surface, serverSide))
        {
            surface.Destroyed += () => _serverSide.Remove(surface);
        }
        else
        {
            _serverSide[surface] = serverSide;
        }

        Changed?.Invoke(surface, serverSide);
    }

    public bool? Preference(Surface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        return _serverSide.TryGetValue(surface, out var serverSide) ? serverSide : null;
    }

    public bool IsServerSide(XdgToplevelWindow toplevel)
    {
        ArgumentNullException.ThrowIfNull(toplevel);
        return Preference(toplevel.Surface)
            ?? (_kde?.ModeOf(toplevel.Surface) == KdeServerDecorationManager.DecorationMode.Server ||
                _xdg?.ModeOf(toplevel) == DecorationMode.ServerSide);
    }
}
