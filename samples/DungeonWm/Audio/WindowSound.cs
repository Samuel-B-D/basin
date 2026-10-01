using System.Numerics;
using Basin.Audio.PipeWire;

namespace DungeonWm;

internal sealed class WindowSound
{
    public WindowSound(DungeonWmWindow window, string via)
    {
        Window = window;
        Id = window.ToplevelId;
        App = window.AppId;
        Via = via;
    }

    public ulong Id { get; }

    public string App { get; }

    public string Via { get; }

    public DungeonWmWindow? Window { get; set; }

    public DungeonWmSnapshot? Snapshot { get; set; }

    public List<AudioStream> Streams { get; } = [];

    public SpatialVoice? Voice { get; set; }

    public MapRegion? Region { get; set; }

    public VoiceParams Wanted { get; set; } = VoiceParams.Centered;

    public SoundPath Path { get; set; }

    public Vector3 Source => Window?.World.Position ?? Snapshot?.World.Position ?? Vector3.Zero;
}
