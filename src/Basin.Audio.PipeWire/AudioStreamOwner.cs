namespace Basin.Audio.PipeWire;

public readonly record struct AudioStreamOwner(ulong ToplevelId, AudioMatchStep Via)
{
    public bool Matched => Via != AudioMatchStep.None;

    public string ViaName => Via switch
    {
        AudioMatchStep.LaunchTag => "launch-tag",
        AudioMatchStep.SecPid => "sec-pid",
        AudioMatchStep.Ancestor => "ancestor",
        AudioMatchStep.Flatpak => "flatpak",
        AudioMatchStep.ReportedPid => "reported-pid",
        _ => "none",
    };
}
