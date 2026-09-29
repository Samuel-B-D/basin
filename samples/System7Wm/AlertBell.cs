using Basin.Capabilities;
using Basin;

namespace System7Wm;

internal sealed class AlertBell(Action ring) : IBell
{
    public void Ring(Surface? surface) => ring();
}
