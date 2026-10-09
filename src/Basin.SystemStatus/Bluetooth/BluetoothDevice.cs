namespace Basin.SystemStatus;

public sealed record BluetoothDevice(string Path, string Name, string Address, string? Icon, bool Paired, bool Connected);
