namespace Basin.Capabilities;

public readonly record struct InputSettingSupport(bool Supported, uint Mask = 0, uint Count = 0)
{
    public static InputSettingSupport None => default;

    public static InputSettingSupport Yes => new(true);
}
