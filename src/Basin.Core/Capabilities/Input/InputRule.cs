namespace Basin.Capabilities;

public sealed class InputRule
{
    public InputMatch Match { get; init; }

    public IReadOnlyList<(InputSetting Setting, InputSettingValue Value)> Settings { get; init; } = [];

    public int Line { get; init; }

    public bool Names(InputSetting setting, out InputSettingValue value)
    {
        for (var i = 0; i < Settings.Count; i++)
        {
            if (Settings[i].Setting == setting)
            {
                value = Settings[i].Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
