namespace DungeonWm;

internal sealed record AudioSettings(
    string Mode = "auto",
    string Hrtf = "/usr/share/libmysofa/default.sofa",
    float ReferenceDistance = 1.0f,
    float Rolloff = 1.0f,
    float MaxDistance = 40.0f,
    float RearLossDb = 3.0f,
    float DoorwayCutoffHz = 2500.0f,
    float DoorwayLossDb = 4.0f,
    float DoorwayOctaves = 1.0f,
    float Reverb = 0.3f,
    float ReverbFloor = 0.05f,
    float ReverbMaxSeconds = 2.0f,
    int MaxVoices = 8,
    int UpdateHz = 30)
{
    public static readonly string[] Modes = ["auto", "hrtf", "pan", "off"];
}
