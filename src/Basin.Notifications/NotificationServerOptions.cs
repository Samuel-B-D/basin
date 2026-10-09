namespace Basin.Notifications;

public sealed record NotificationServerOptions
{
    public const int DefaultExpireMilliseconds = 5000;

    public string? Address { get; init; }

    public bool Replace { get; init; }

    public bool AllowReplacement { get; init; }

    public int DefaultExpireMs { get; init; } = DefaultExpireMilliseconds;

    public string ServerName { get; init; } = "basin";

    public string Vendor { get; init; } = "basin";

    public string Version { get; init; } = "1.0";

    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(5);
}
