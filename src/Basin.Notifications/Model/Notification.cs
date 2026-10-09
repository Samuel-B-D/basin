namespace Basin.Notifications;

public sealed record Notification
{
    public uint Id { get; init; }

    public string AppName { get; init; } = "";

    public string AppIcon { get; init; } = "";

    public string Summary { get; init; } = "";

    public string Body { get; init; } = "";

    public IReadOnlyList<NotificationSpan> BodySpans { get; init; } = [];

    public IReadOnlyList<NotificationAction> Actions { get; init; } = [];

    public NotificationUrgency Urgency { get; init; } = NotificationUrgency.Normal;

    public string? Category { get; init; }

    public string? DesktopEntry { get; init; }

    public NotificationImage? Image { get; init; }

    public string? ImagePath { get; init; }

    public string? SoundFile { get; init; }

    public string? SoundName { get; init; }

    public bool SuppressSound { get; init; }

    public bool Resident { get; init; }

    public bool Transient { get; init; }

    public bool ActionIcons { get; init; }

    public int ExpireTimeout { get; init; } = -1;

    public TimeSpan? ExpiresAfter { get; init; }

    public string? Sender { get; init; }

    public bool HasAction(string key)
    {
        foreach (var action in Actions)
        {
            if (action.Key == key)
            {
                return true;
            }
        }

        return false;
    }
}
