using Basin.Capabilities;

namespace LunaWm;

internal sealed class LunaLockState : ILockState
{
    private readonly LockStateObservers _observers = new();

    public bool IsLocked { get; private set; }

    public void AddObserver(ILockStateObserver observer) => _observers.Add(observer);

    public void RemoveObserver(ILockStateObserver observer) => _observers.Remove(observer);

    public void Set(bool locked)
    {
        if (locked == IsLocked)
        {
            return;
        }

        IsLocked = locked;
        if (locked)
        {
            _observers.Locked();
        }
        else
        {
            _observers.Unlocked();
        }
    }
}
