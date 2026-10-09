using Tmds.DBus.Protocol;

namespace Basin.SystemStatus;

internal static class DBusProperties
{
    public const string Interface = "org.freedesktop.DBus.Properties";

    public static MessageBuffer Method(DBusConnection connection, string service, string path, string @interface, string member)
    {
        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(service, path, @interface, member);
        return writer.CreateMessage();
    }

    public static Task<Dictionary<string, VariantValue>> GetAllAsync(DBusConnection connection, string service, string path, string @interface)
    {
        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(service, path, Interface, "GetAll", "s");
        writer.WriteString(@interface);
        return connection.CallMethodAsync(writer.CreateMessage(), static (message, _) => message.GetBodyReader().ReadDictionaryOfStringToVariantValue(), null);
    }

    public static Task<VariantValue> GetAsync(DBusConnection connection, string service, string path, string @interface, string name)
    {
        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(service, path, Interface, "Get", "ss");
        writer.WriteString(@interface);
        writer.WriteString(name);
        return connection.CallMethodAsync(writer.CreateMessage(), static (message, _) => Unwrap(message.GetBodyReader().ReadVariantValue()), null);
    }

    public static MessageBuffer SetBool(DBusConnection connection, string service, string path, string @interface, string name, bool value)
    {
        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(service, path, Interface, "Set", "ssv");
        writer.WriteString(@interface);
        writer.WriteString(name);
        writer.WriteVariantBool(value);
        return writer.CreateMessage();
    }

    public static VariantValue Unwrap(VariantValue value) => value.Type == VariantValueType.Variant ? Unwrap(value.GetVariantValue()) : value;

    public static bool Bool(IReadOnlyDictionary<string, VariantValue> map, string key, bool fallback = false) =>
        map.TryGetValue(key, out var raw) && Unwrap(raw) is { Type: VariantValueType.Bool } value ? value.GetBool() : fallback;

    public static double Double(IReadOnlyDictionary<string, VariantValue> map, string key) =>
        map.TryGetValue(key, out var raw) && Unwrap(raw) is { Type: VariantValueType.Double } value ? value.GetDouble() : 0;

    public static uint UInt(IReadOnlyDictionary<string, VariantValue> map, string key) =>
        map.TryGetValue(key, out var raw) ? UInt(Unwrap(raw)) : 0;

    public static uint UInt(VariantValue value) => value.Type switch
    {
        VariantValueType.UInt32 => value.GetUInt32(),
        VariantValueType.Byte => value.GetByte(),
        VariantValueType.Int32 => (uint)Math.Max(0, value.GetInt32()),
        _ => 0,
    };

    public static long Long(IReadOnlyDictionary<string, VariantValue> map, string key)
    {
        if (!map.TryGetValue(key, out var raw))
        {
            return 0;
        }

        var value = Unwrap(raw);
        return value.Type switch
        {
            VariantValueType.Int64 => value.GetInt64(),
            VariantValueType.UInt64 => (long)Math.Min(value.GetUInt64(), long.MaxValue),
            VariantValueType.Int32 => value.GetInt32(),
            VariantValueType.UInt32 => value.GetUInt32(),
            _ => 0,
        };
    }

    public static string? String(IReadOnlyDictionary<string, VariantValue> map, string key) =>
        map.TryGetValue(key, out var raw) && Unwrap(raw) is { Type: VariantValueType.String or VariantValueType.ObjectPath } value ? value.GetString() : null;

    public static string? Path(IReadOnlyDictionary<string, VariantValue> map, string key) =>
        map.TryGetValue(key, out var raw) && Unwrap(raw) is { Type: VariantValueType.ObjectPath } value ? value.GetObjectPathAsString() : null;

    public static string[] Paths(IReadOnlyDictionary<string, VariantValue> map, string key) =>
        map.TryGetValue(key, out var raw) && Unwrap(raw) is { Type: VariantValueType.Array } value ? value.GetArray<string>() : [];

    public static byte[] Bytes(IReadOnlyDictionary<string, VariantValue> map, string key) =>
        map.TryGetValue(key, out var raw) && Unwrap(raw) is { Type: VariantValueType.Array } value ? value.GetArray<byte>() : [];
}
