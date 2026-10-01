namespace Basin.Audio.PipeWire;

public readonly record struct SoundPlacement(float GainDb, float Azimuth = 0, float CutoffHz = VoiceParams.OpenCutoffHz, float Rate = 1)
{
    public static SoundPlacement Centered => new(0);
}
