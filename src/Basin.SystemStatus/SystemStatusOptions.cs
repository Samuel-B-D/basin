namespace Basin.SystemStatus;

public sealed class SystemStatusOptions
{
    public string? Address { get; init; }

    public int CoalesceMilliseconds { get; init; } = 50;
}
