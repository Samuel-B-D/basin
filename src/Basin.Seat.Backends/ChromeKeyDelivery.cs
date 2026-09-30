using Basin.Scene;

namespace Basin.Seat.Backends;

public sealed class ChromeKeyDelivery
{
    private readonly Basin.Seat.Seat _seat;
    private readonly UISurfaceRouter _router;

    public ChromeKeyDelivery(Basin.Seat.Seat seat, UISurfaceRouter router)
    {
        ArgumentNullException.ThrowIfNull(seat);
        ArgumentNullException.ThrowIfNull(router);
        _seat = seat;
        _router = router;
    }

    public bool CommitText { get; init; } = true;

    public bool Deliver(uint timeMs, uint key, bool pressed)
    {
        if (!Chrome(timeMs, key, pressed))
        {
            _seat.Keyboard.NotifyKey(timeMs, key, pressed);
            return false;
        }

        return true;
    }

    public bool Chrome(uint timeMs, uint key, bool pressed)
    {
        if (!_router.Key(timeMs, key, pressed))
        {
            return false;
        }

        if (pressed && CommitText)
        {
            Span<char> text = stackalloc char[16];
            var length = _seat.Keyboard.TextFor(key, text);
            if (length > 0)
            {
                _router.TextCommit(text[..length]);
            }
        }

        _seat.Keyboard.NotifyKeyConsumed(key, pressed);
        return true;
    }
}
