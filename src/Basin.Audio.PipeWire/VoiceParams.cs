namespace Basin.Audio.PipeWire;

public readonly record struct VoiceParams(
    float Azimuth,
    float Elevation,
    float Radius,
    float GainDb,
    float CutoffHz,
    float WetDb)
{
    public const float SilentDb = -90;

    public const float OpenCutoffHz = 20000;

    public static VoiceParams Centered => new(0, 0, 1, 0, OpenCutoffHz, SilentDb);
}
