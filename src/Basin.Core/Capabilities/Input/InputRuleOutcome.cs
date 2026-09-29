namespace Basin.Capabilities;

public readonly record struct InputRuleOutcome(
    int Rule,
    int Line,
    ulong Device,
    string? DeviceName,
    InputSetting Setting,
    InputSettingResult Result);
