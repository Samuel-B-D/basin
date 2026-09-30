namespace Basin.Seat.Backends;

public sealed class ButtonLatch
{
    private readonly Basin.Seat.Seat _seat;
    private readonly uint[] _seatButtons = new uint[16];
    private int _seatCount;

    public ButtonLatch(Basin.Seat.Seat seat)
    {
        ArgumentNullException.ThrowIfNull(seat);
        _seat = seat;
    }

    public int Held { get; private set; }

    public bool SeatHolds => _seatCount > 0;

    public bool Press() => Held++ == 0;

    public bool Release()
    {
        if (Held > 0)
        {
            Held--;
        }

        if (Held > 0)
        {
            return false;
        }

        _seatCount = 0;
        return true;
    }

    public void SeatButton(uint timeMs, uint button, bool pressed)
    {
        var index = Array.IndexOf(_seatButtons, button, 0, _seatCount);
        if (pressed)
        {
            if (index < 0 && _seatCount < _seatButtons.Length)
            {
                _seatButtons[_seatCount++] = button;
            }

            _seat.Pointer.NotifyButton(timeMs, button, true);
            return;
        }

        if (index < 0)
        {
            return;
        }

        _seatButtons[index] = _seatButtons[--_seatCount];
        _seat.Pointer.NotifyButton(timeMs, button, false);
    }
}
