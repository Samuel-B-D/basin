namespace Basin.SystemStatus;

public sealed record NetworkAccessPoint(string Path, string Ssid, byte Strength, AccessPointSecurity Security, bool IsActive, NetworkSavedConnection? Saved);
