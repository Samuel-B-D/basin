namespace Basin.Notifications;

public readonly record struct NotificationAction(string Key, string Label)
{
    public const string DefaultKey = "default";

    public bool IsDefault => Key == DefaultKey;
}
