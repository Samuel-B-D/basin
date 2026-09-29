namespace Basin.Pty;

public readonly record struct PtyExit(int? Status, int? Signal)
{
    public bool Succeeded => Status == 0;

    internal static PtyExit FromWaitStatus(int status) => (status & 0x7f) == 0
        ? new PtyExit((status >> 8) & 0xff, null)
        : new PtyExit(null, status & 0x7f);
}
