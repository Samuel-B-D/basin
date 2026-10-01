using System.Numerics;
using Basin.Audio.PipeWire;
using Xunit;

namespace DungeonWm.Tests;

public sealed class AcousticsTests
{
    private static readonly AudioSettings Settings = new();

    [Theory]
    [InlineData(-1, 0, 0, 90)]
    [InlineData(1, 0, 0, 270)]
    [InlineData(0, 0, -1, 0)]
    [InlineData(0, 0, 1, 180)]
    public void Azimuth_counts_counterclockwise_from_straight_ahead(float x, float y, float z, float expected)
    {
        var (azimuth, elevation) = Acoustics.Direction(Vector3.Zero, Quaternion.Identity, new Vector3(x, y, z));

        Assert.Equal(expected, azimuth, 3);
        Assert.Equal(0, elevation, 3);
    }

    [Fact]
    public void Turning_the_camera_moves_the_source_the_other_way()
    {
        var camera = new Basin.Scene3D.WorldCamera { Position = new Vector3(2, 1.6f, 5), Yaw = 0 };
        var ahead = new Vector3(2, 1.6f, 1);

        Assert.Equal(0, Acoustics.Direction(camera.Position, camera.Orientation, ahead).Azimuth, 3);
        camera.Yaw = MathF.PI / 2;
        Assert.Equal(270, Acoustics.Direction(camera.Position, camera.Orientation, ahead).Azimuth, 3);
        camera.Yaw = -MathF.PI / 2;
        Assert.Equal(90, Acoustics.Direction(camera.Position, camera.Orientation, ahead).Azimuth, 3);
    }

    [Fact]
    public void Elevation_is_positive_above_the_ear()
    {
        var (_, elevation) = Acoustics.Direction(Vector3.Zero, Quaternion.Identity, new Vector3(0, 1, -1));

        Assert.Equal(45, elevation, 3);
    }

    [Fact]
    public void Gain_falls_by_the_inverse_distance_law_and_stops_at_the_reference()
    {
        var near = Acoustics.Place(Vector3.Zero, Quaternion.Identity, SoundPath.Direct(Vector3.Zero, new Vector3(0, 0, -0.5f)), Settings, pan: false);
        var twice = Acoustics.Place(Vector3.Zero, Quaternion.Identity, SoundPath.Direct(Vector3.Zero, new Vector3(0, 0, -2)), Settings, pan: false);
        var far = Acoustics.Place(Vector3.Zero, Quaternion.Identity, SoundPath.Direct(Vector3.Zero, new Vector3(0, 0, -41)), Settings, pan: false);

        Assert.Equal(0, near.GainDb, 3);
        Assert.Equal(-6.021, twice.GainDb, 2);
        Assert.Equal(VoiceParams.SilentDb, far.GainDb);
    }

    [Fact]
    public void A_source_behind_loses_the_rear_decibels_in_pan_mode_only()
    {
        var behind = SoundPath.Direct(Vector3.Zero, new Vector3(0, 0, 1));

        Assert.Equal(-Settings.RearLossDb, Acoustics.Place(Vector3.Zero, Quaternion.Identity, behind, Settings, pan: true).GainDb, 3);
        Assert.Equal(0, Acoustics.Place(Vector3.Zero, Quaternion.Identity, behind, Settings, pan: false).GainDb, 3);
    }
}
