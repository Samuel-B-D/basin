using System.Numerics;
using Basin.Audio.PipeWire;

namespace DungeonWm;

internal static class Acoustics
{
    public static VoiceParams Place(Vector3 listener, Quaternion orientation, in SoundPath path, AudioSettings settings, bool pan)
    {
        if (!path.Audible || path.Length > settings.MaxDistance)
        {
            return VoiceParams.Centered with { GainDb = VoiceParams.SilentDb };
        }

        var (azimuth, elevation) = Direction(listener, orientation, path.Apparent);
        var reference = MathF.Max(0.05f, settings.ReferenceDistance);
        var distance = MathF.Max(reference, path.Length);
        var gain = -20 * settings.Rolloff * MathF.Log10(distance / reference);
        if (pan)
        {
            gain -= settings.RearLossDb * MathF.Max(0, -MathF.Cos(azimuth * MathF.PI / 180));
        }

        var cutoff = VoiceParams.OpenCutoffHz;
        if (path.Doorways > 0)
        {
            gain -= settings.DoorwayLossDb * (path.Doorways - 1);
            cutoff = settings.DoorwayCutoffHz / MathF.Pow(2, settings.DoorwayOctaves * (path.Doorways - 1));
        }

        return new VoiceParams(azimuth, elevation, path.Length, gain, cutoff, WetDb(gain, settings));
    }

    public const float StoneAbsorption = 0.25f;

    public static float WetDb(float gainDb, AudioSettings settings)
    {
        if (settings.Reverb <= 0 || gainDb <= VoiceParams.SilentDb)
        {
            return VoiceParams.SilentDb;
        }

        var dry = MathF.Pow(10, gainDb / 20);
        var wet = settings.Reverb * MathF.Max(dry, settings.ReverbFloor);
        return 20 * MathF.Log10(wet / dry);
    }

    public static float ReverbSeconds(MapRegion region, float unit, float maxSeconds)
    {
        var cell = 4 * unit;
        var height = RoomBaker.WallHeight * unit;
        var floor = region.Cells.Count * cell * cell;
        var cells = new HashSet<GridCell>(region.Cells);
        var edges = 0;
        foreach (var at in region.Cells)
        {
            foreach (var side in GridSides.All)
            {
                edges += cells.Contains(at.Step(side)) ? 0 : 1;
            }
        }

        var volume = floor * height;
        var surface = (2 * floor) + (edges * cell * height);
        var sabine = surface > 0 ? 0.161f * volume / (surface * StoneAbsorption) : 0;
        return Math.Clamp(sabine, RoomImpulse.SecondsQuantum, maxSeconds);
    }

    public static (float Azimuth, float Elevation) Direction(Vector3 listener, Quaternion orientation, Vector3 source)
    {
        var relative = source - listener;
        var right = Vector3.Dot(relative, Vector3.Transform(Vector3.UnitX, orientation));
        var up = Vector3.Dot(relative, Vector3.Transform(Vector3.UnitY, orientation));
        var forward = Vector3.Dot(relative, Vector3.Transform(-Vector3.UnitZ, orientation));
        var azimuth = MathF.Atan2(-right, forward) * 180 / MathF.PI;
        if (azimuth < 0)
        {
            azimuth += 360;
        }

        var elevation = MathF.Atan2(up, MathF.Sqrt((right * right) + (forward * forward))) * 180 / MathF.PI;
        return (azimuth, elevation);
    }

    public static float Signed(float azimuth) => azimuth > 180 ? azimuth - 360 : azimuth;
}
