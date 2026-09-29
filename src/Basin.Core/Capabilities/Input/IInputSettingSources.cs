namespace Basin.Capabilities;

public interface IInputSettingSources
{
    InputSettingLayer SourceOf(ulong deviceId, InputSetting setting);

    IReadOnlyList<int> RulesFor(ulong deviceId);
}
