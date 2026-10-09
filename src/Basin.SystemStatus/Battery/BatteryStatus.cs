using Tmds.DBus.Protocol;

namespace Basin.SystemStatus;

public sealed class BatteryStatus : SystemStatusReader
{
    public const string ServiceName = "org.freedesktop.UPower";
    public const string DisplayDevicePath = "/org/freedesktop/UPower/devices/DisplayDevice";

    private const string DeviceInterface = "org.freedesktop.UPower.Device";
    private const uint BatteryType = 2;

    private BatteryStatus(SystemStatusBus bus)
        : base(bus, ServiceName, "battery")
    {
    }

    public bool IsPresent { get; private set; }

    public double Percentage { get; private set; }

    public BatteryState State { get; private set; }

    public TimeSpan TimeToEmpty { get; private set; }

    public TimeSpan TimeToFull { get; private set; }

    public string? IconName { get; private set; }

    private protected override string? AvailableReason => IsPresent ? null : "UPower reports no battery";

    public static BatteryStatus? TryCreate(SystemStatusBus bus, out string? reason)
    {
        if (!CheckBus(bus, out reason))
        {
            return null;
        }

        var status = new BatteryStatus(bus);
        status.Start();
        return status;
    }

    private protected override MatchRule[] Rules() =>
    [
        new()
        {
            Type = MessageType.Signal,
            Path = DisplayDevicePath,
            Interface = DBusProperties.Interface,
            Member = "PropertiesChanged",
        },
    ];

    private protected override async Task<Action> ReadAsync(DBusConnection connection)
    {
        var map = await DBusProperties.GetAllAsync(connection, ServiceName, DisplayDevicePath, DeviceInterface).ConfigureAwait(false);
        var type = DBusProperties.UInt(map, "Type");
        var present = DBusProperties.Bool(map, "IsPresent") && type == BatteryType;
        var percentage = Math.Clamp(DBusProperties.Double(map, "Percentage"), 0, 100);
        var stateValue = DBusProperties.UInt(map, "State");
        var state = stateValue <= (uint)BatteryState.PendingDischarge ? (BatteryState)stateValue : BatteryState.Unknown;
        var toEmpty = TimeSpan.FromSeconds(Math.Max(0, DBusProperties.Long(map, "TimeToEmpty")));
        var toFull = TimeSpan.FromSeconds(Math.Max(0, DBusProperties.Long(map, "TimeToFull")));
        var icon = DBusProperties.String(map, "IconName");
        return () =>
        {
            IsPresent = present;
            Percentage = present ? percentage : 0;
            State = present ? state : BatteryState.Unknown;
            TimeToEmpty = present ? toEmpty : TimeSpan.Zero;
            TimeToFull = present ? toFull : TimeSpan.Zero;
            IconName = icon is { Length: > 0 } ? icon : null;
        };
    }

    private protected override void Clear()
    {
        IsPresent = false;
        Percentage = 0;
        State = BatteryState.Unknown;
        TimeToEmpty = TimeSpan.Zero;
        TimeToFull = TimeSpan.Zero;
        IconName = null;
    }
}
