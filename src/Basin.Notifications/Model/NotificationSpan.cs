namespace Basin.Notifications;

public readonly record struct NotificationSpan(int Start, int Length, NotificationSpanKind Kind, string? Target = null);
