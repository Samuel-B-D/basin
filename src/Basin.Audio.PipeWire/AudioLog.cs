using Basin.Diagnostics;

namespace Basin.Audio.PipeWire;

internal static class AudioLog
{
    public static readonly BasinLogger Log = BasinLog.For("audio");
}
