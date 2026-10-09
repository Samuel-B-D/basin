namespace Basin.SoftKeyboard.Fcitx5;

public sealed record Fcitx5KeyboardOptions
{
    public string? Address { get; init; }

    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(2);
}
