using Tmds.DBus.Protocol;

namespace Basin.Tests;

internal sealed class TestDbusMenuServer : IPathMethodHandler, IDisposable
{
    public const string ObjectPath = "/MenuBar/1";

    private readonly DBusConnection _connection;
    private uint _revision = 1;

    private TestDbusMenuServer(DBusConnection connection)
    {
        _connection = connection;
    }

    public string Path => ObjectPath;

    public bool HandlesChildPaths => false;

    public string UniqueName { get; private set; } = "";

    public List<TestMenuNode> Root { get; } = [];

    public List<(int Id, string EventId)> Events { get; } = [];

    public List<int> AboutToShow { get; } = [];

    public bool AboutToShowNeedsUpdate { get; set; }

    public static async Task<TestDbusMenuServer> StartAsync(string address)
    {
        var connection = new DBusConnection(address);
        await connection.ConnectAsync();
        var server = new TestDbusMenuServer(connection);
        connection.AddMethodHandler(server);
        server.UniqueName = connection.UniqueName ?? "";
        return server;
    }

    public void EmitLayoutUpdated()
    {
        _revision++;
        using var writer = _connection.GetMessageWriter();
        writer.WriteSignalHeader(null, ObjectPath, "com.canonical.dbusmenu", "LayoutUpdated", "ui");
        writer.WriteUInt32(_revision);
        writer.WriteInt32(0);
        _ = _connection.TrySendMessage(writer.CreateMessage());
    }

    public ValueTask HandleMethodAsync(MethodContext context)
    {
        var request = context.Request;
        var member = request.MemberAsString;
        var reader = request.GetBodyReader();
        switch (member)
        {
            case "GetLayout":
                {
                    var writer = context.CreateReplyWriter("u(ia{sv}av)");
                    try
                    {
                        writer.WriteUInt32(_revision);
                        WriteNode(ref writer, new TestMenuNode(0, "") { Children = Root }, root: true);
                        context.Reply(writer.CreateMessage());
                    }
                    finally
                    {
                        writer.Dispose();
                    }

                    break;
                }

            case "AboutToShow":
                {
                    lock (AboutToShow)
                    {
                        AboutToShow.Add(reader.ReadInt32());
                    }

                    using var writer = context.CreateReplyWriter("b");
                    writer.WriteBool(AboutToShowNeedsUpdate);
                    context.Reply(writer.CreateMessage());
                    break;
                }

            case "Event":
                {
                    var id = reader.ReadInt32();
                    var eventId = reader.ReadString();
                    lock (Events)
                    {
                        Events.Add((id, eventId));
                    }

                    if (!context.NoReplyExpected)
                    {
                        using var writer = context.CreateReplyWriter(null);
                        context.Reply(writer.CreateMessage());
                    }

                    break;
                }

            default:
                context.ReplyUnknownMethodError();
                break;
        }

        return ValueTask.CompletedTask;
    }

    public void Dispose() => _connection.Dispose();

    private static void WriteNode(ref MessageWriter writer, TestMenuNode node, bool root)
    {
        if (!root)
        {
            writer.WriteSignature("(ia{sv}av)");
        }

        writer.WriteStructureStart();
        writer.WriteInt32(node.Id);
        var properties = writer.WriteDictionaryStart();
        if (!root)
        {
            Text(ref writer, "label", node.Label);
            if (!node.Enabled)
            {
                writer.WriteDictionaryEntryStart();
                writer.WriteString("enabled");
                writer.WriteVariantBool(false);
            }

            if (node.Separator)
            {
                Text(ref writer, "type", "separator");
            }

            if (node.Toggle is { } toggle)
            {
                Text(ref writer, "toggle-type", toggle);
                writer.WriteDictionaryEntryStart();
                writer.WriteString("toggle-state");
                writer.WriteVariantInt32(node.Checked ? 1 : 0);
            }

            if (node.Shortcut is { } shortcut)
            {
                writer.WriteDictionaryEntryStart();
                writer.WriteString("shortcut");
                writer.WriteSignature("aas");
                var outer = writer.WriteArrayStart(DBusType.Array);
                writer.WriteArray(shortcut);
                writer.WriteArrayEnd(outer);
            }

            if (node.Children.Count > 0)
            {
                Text(ref writer, "children-display", "submenu");
            }
        }

        writer.WriteDictionaryEnd(properties);
        var children = writer.WriteArrayStart(DBusType.Variant);
        foreach (var child in node.Children)
        {
            WriteNode(ref writer, child, root: false);
        }

        writer.WriteArrayEnd(children);
    }

    private static void Text(ref MessageWriter writer, string key, string value)
    {
        writer.WriteDictionaryEntryStart();
        writer.WriteString(key);
        writer.WriteVariantString(value);
    }
}

internal sealed record TestMenuNode(int Id, string Label)
{
    public bool Enabled { get; init; } = true;

    public bool Separator { get; init; }

    public string? Toggle { get; init; }

    public bool Checked { get; init; }

    public string[]? Shortcut { get; init; }

    public List<TestMenuNode> Children { get; init; } = [];
}
