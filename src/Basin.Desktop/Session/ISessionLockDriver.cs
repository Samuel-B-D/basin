namespace Basin.Desktop;

public interface ISessionLockDriver
{
    void Attach(SessionLockManager manager);
}
