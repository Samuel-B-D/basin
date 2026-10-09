namespace Basin.SystemStatus;

public enum BatteryState
{
    Unknown = 0,
    Charging = 1,
    Discharging = 2,
    Empty = 3,
    Full = 4,
    PendingCharge = 5,
    PendingDischarge = 6,
}
