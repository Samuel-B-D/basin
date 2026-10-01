namespace Basin.Audio.PipeWire;

public enum AudioMatchStep
{
    None,
    LaunchTag,
    SecPid,
    Ancestor,
    Flatpak,
    ReportedPid,
}
