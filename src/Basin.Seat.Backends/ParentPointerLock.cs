using Basin.Backend.Wayland;

namespace Basin.Seat.Backends;

public sealed class ParentPointerLock
{
    private readonly OutputLayout _layout;
    private readonly List<(object Owner, WaylandOutput Output)> _requests = [];

    public ParentPointerLock(OutputLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        _layout = layout;
    }

    public WaylandOutput? Output { get; private set; }

    public bool IsLocked => Output is not null;

    public bool IsRequested(object owner)
    {
        foreach (var request in _requests)
        {
            if (ReferenceEquals(request.Owner, owner))
            {
                return true;
            }
        }

        return false;
    }

    public bool Request(object owner, WaylandOutput output)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(output);

        RemoveRequest(owner);
        _requests.Add((owner, output));
        Apply();
        return ReferenceEquals(Output, output) && IsRequested(owner);
    }

    public void Release(object owner, double layoutX, double layoutY)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (!RemoveRequest(owner))
        {
            return;
        }

        if (_requests.Count > 0)
        {
            Apply();
            return;
        }

        if (Output is { } output)
        {
            var box = _layout.BoxOf(output);
            output.SetCursorPositionHint((layoutX - box.X) * output.Scale, (layoutY - box.Y) * output.Scale);
            output.LockPointer(false);
            Output = null;
        }
    }

    public void Forget(IOutput output)
    {
        if (ReferenceEquals(Output, output))
        {
            Output = null;
        }

        for (var i = _requests.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(_requests[i].Output, output))
            {
                _requests.RemoveAt(i);
            }
        }

        if (Output is null && _requests.Count > 0)
        {
            Apply();
        }
    }

    private void Apply()
    {
        while (_requests.Count > 0)
        {
            var (owner, wanted) = _requests[^1];
            if (ReferenceEquals(Output, wanted))
            {
                return;
            }

            if (Output is { } previous)
            {
                previous.LockPointer(false);
                Output = null;
            }

            if (wanted.LockPointer(true))
            {
                Output = wanted;
                return;
            }

            RemoveRequest(owner);
        }

        if (Output is { } stale)
        {
            stale.LockPointer(false);
            Output = null;
        }
    }

    private bool RemoveRequest(object owner)
    {
        for (var i = 0; i < _requests.Count; i++)
        {
            if (ReferenceEquals(_requests[i].Owner, owner))
            {
                _requests.RemoveAt(i);
                return true;
            }
        }

        return false;
    }
}
