namespace Basin.Capabilities;

public readonly record struct InputDeviceInfo(
    ulong Id,
    string Name,
    InputDeviceCapability Capabilities,
    InputDeviceType Type,
    string? OutputName,
    string? Sysname = null,
    uint Vendor = 0,
    uint Product = 0,
    uint Bus = 0);
