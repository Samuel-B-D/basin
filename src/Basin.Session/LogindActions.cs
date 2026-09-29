using Basin.Diagnostics;
using Tmds.DBus.Protocol;

namespace Basin.Session;

public static class LogindActions
{
    private const string Service = "org.freedesktop.login1";
    private const string ObjectPath = "/org/freedesktop/login1";
    private const string Interface = "org.freedesktop.login1.Manager";

    private static readonly BasinLogger Log = BasinLog.For("logind");

    public static bool Available => Directory.Exists("/run/systemd/system");

    public static Task<bool> RunAsync(LogindAction action, string? address = null) => CallAsync(Method(action), address);

    public static async Task<bool> CanAsync(LogindAction action, string? address = null)
    {
        try
        {
            if ((address ?? DBusAddress.System) is not { } bus)
            {
                return false;
            }

            using var connection = new DBusConnection(bus);
            await connection.ConnectAsync().ConfigureAwait(false);
            var answer = await connection.CallMethodAsync(
                CanRequest(connection, "Can" + Method(action)),
                static (message, _) => message.GetBodyReader().ReadString(),
                null).ConfigureAwait(false);
            return answer is "yes" or "challenge";
        }
        catch (Exception error) when (error is DBusExceptionBase or InvalidOperationException or IOException)
        {
            Log.Debug($"logind Can{Method(action)} failed: {error.Message}");
            return false;
        }
    }

    private static string Method(LogindAction action) => action switch
    {
        LogindAction.Reboot => "Reboot",
        LogindAction.Suspend => "Suspend",
        _ => "PowerOff",
    };

    private static MessageBuffer Request(DBusConnection connection, string method)
    {
        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(Service, ObjectPath, Interface, method, "b");
        writer.WriteBool(false);
        return writer.CreateMessage();
    }

    private static MessageBuffer CanRequest(DBusConnection connection, string method)
    {
        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(Service, ObjectPath, Interface, method);
        return writer.CreateMessage();
    }

    private static async Task<bool> CallAsync(string method, string? address)
    {
        try
        {
            if ((address ?? DBusAddress.System) is not { } bus)
            {
                Log.Warn($"no system bus for {method}");
                return false;
            }

            using var connection = new DBusConnection(bus);
            await connection.ConnectAsync().ConfigureAwait(false);
            await connection.CallMethodAsync(Request(connection, method)).ConfigureAwait(false);
            return true;
        }
        catch (Exception error) when (error is DBusExceptionBase or InvalidOperationException or IOException)
        {
            Log.Warn($"logind {method} failed: {error.Message}");
            return false;
        }
    }
}
