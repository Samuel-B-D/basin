using Basin.Notifications;
using Tmds.DBus.Protocol;

namespace Basin.Tests;

internal sealed class NotificationTestClient : IDisposable
{
    public delegate void HintWriter(ref MessageWriter writer);

    private const string Interface = "org.freedesktop.Notifications";

    private readonly List<(string Member, uint Id, string Value)> _signals = [];
    private readonly Lock _gate = new();
    private IDisposable? _match;

    private NotificationTestClient(DBusConnection connection)
    {
        Connection = connection;
    }

    public DBusConnection Connection { get; }

    public string UniqueName => Connection.UniqueName!;

    public static NotificationTestClient Connect(CompositorTestHost host, string address)
    {
        var connection = new DBusConnection(address);
        PortalBusTests.Await(host, connection.ConnectAsync().AsTask());
        var client = new NotificationTestClient(connection);
        var rule = new MatchRule { Type = MessageType.Signal, Interface = Interface, Path = NotificationServer.ObjectPath };
        client._match = PortalBusTests.Await(host, connection.AddMatchAsync(
            rule,
            static (Message message, object? _) =>
            {
                var reader = message.GetBodyReader();
                var id = reader.ReadUInt32();
                var value = message.SignatureAsString == "uu"
                    ? reader.ReadUInt32().ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : reader.ReadString();
                return (message.MemberAsString ?? "", id, value);
            },
            (Notification<(string, uint, string)> signal) =>
            {
                if (signal.HasValue)
                {
                    lock (client._gate)
                    {
                        client._signals.Add(signal.Value);
                    }
                }
            },
            emitOnCapturedContext: false).AsTask());
        return client;
    }

    public List<(string Member, uint Id, string Value)> Signals()
    {
        lock (_gate)
        {
            return [.. _signals];
        }
    }

    public Task<uint> NotifyAsync(
        string summary,
        string body = "",
        uint replacesId = 0,
        string[]? actions = null,
        int expireTimeout = -1,
        HintWriter? hints = null,
        string appName = "notify-test",
        string appIcon = "")
    {
        var writer = Connection.GetMessageWriter();
        writer.WriteMethodCallHeader(NotificationServer.BusName, NotificationServer.ObjectPath, Interface, "Notify", "susssasa{sv}i");
        writer.WriteString(appName);
        writer.WriteUInt32(replacesId);
        writer.WriteString(appIcon);
        writer.WriteString(summary);
        writer.WriteString(body);
        writer.WriteArray(actions ?? []);
        var dictionary = writer.WriteDictionaryStart();
        hints?.Invoke(ref writer);
        writer.WriteDictionaryEnd(dictionary);
        writer.WriteInt32(expireTimeout);
        var message = writer.CreateMessage();
        writer.Dispose();
        return Connection.CallMethodAsync(message, static (Message message, object? _) => message.GetBodyReader().ReadUInt32(), null);
    }

    public Task CloseAsync(uint id)
    {
        using var writer = Connection.GetMessageWriter();
        writer.WriteMethodCallHeader(NotificationServer.BusName, NotificationServer.ObjectPath, Interface, "CloseNotification", "u");
        writer.WriteUInt32(id);
        return Connection.CallMethodAsync(writer.CreateMessage());
    }

    public Task<string[]> CapabilitiesAsync()
    {
        using var writer = Connection.GetMessageWriter();
        writer.WriteMethodCallHeader(NotificationServer.BusName, NotificationServer.ObjectPath, Interface, "GetCapabilities");
        return Connection.CallMethodAsync(writer.CreateMessage(), static (Message message, object? _) => message.GetBodyReader().ReadArrayOfString(), null);
    }

    public Task<(string Name, string Vendor, string Version, string Spec)> ServerInformationAsync()
    {
        using var writer = Connection.GetMessageWriter();
        writer.WriteMethodCallHeader(NotificationServer.BusName, NotificationServer.ObjectPath, Interface, "GetServerInformation");
        return Connection.CallMethodAsync(writer.CreateMessage(), static (Message message, object? _) =>
        {
            var reader = message.GetBodyReader();
            return (reader.ReadString(), reader.ReadString(), reader.ReadString(), reader.ReadString());
        }, null);
    }

    public static void StringHint(ref MessageWriter writer, string key, string value)
    {
        writer.WriteDictionaryEntryStart();
        writer.WriteString(key);
        writer.WriteVariantString(value);
    }

    public static void BoolHint(ref MessageWriter writer, string key, bool value)
    {
        writer.WriteDictionaryEntryStart();
        writer.WriteString(key);
        writer.WriteVariantBool(value);
    }

    public static void ByteHint(ref MessageWriter writer, string key, byte value)
    {
        writer.WriteDictionaryEntryStart();
        writer.WriteString(key);
        writer.WriteVariantByte(value);
    }

    public static void ImageHint(ref MessageWriter writer, string key, int width, int height, int stride, bool alpha, int channels, byte[] pixels)
    {
        writer.WriteDictionaryEntryStart();
        writer.WriteString(key);
        writer.WriteSignature("(iiibiiay)");
        writer.WriteStructureStart();
        writer.WriteInt32(width);
        writer.WriteInt32(height);
        writer.WriteInt32(stride);
        writer.WriteBool(alpha);
        writer.WriteInt32(8);
        writer.WriteInt32(channels);
        writer.WriteArray(pixels);
    }

    public void Dispose()
    {
        _match?.Dispose();
        Connection.Dispose();
    }
}
