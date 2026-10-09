using Basin.Audio.PipeWire;
using PipeWire.Native;
using PipeWire.Spa;
using Xunit;
using static PipeWire.Native.Pipewire;

namespace Basin.Tests;

[Collection(AudioDaemonCollection.Name)]
public sealed class SinkVolumeTests(AudioDaemonFixture daemon)
{
    private const string DefaultSinkKey = "default.audio.sink";

    [Fact]
    public void The_default_sink_volume_and_mute_read_and_write_through_the_node_props()
    {
        daemon.SkipIfUnavailable();
        using var rig = new AudioRig(daemon);
        Assert.True(rig.Session.WaitFor(() => rig.Session.DefaultMetadata is not null, 5000));
        _ = rig.Node(AudioDaemonFixture.SinkName);
        rig.Session.DefaultMetadata!.SetProperty(0, DefaultSinkKey, "Spa:String:JSON", $$"""{"name":"{{AudioDaemonFixture.SinkName}}"}""");
        try
        {
            using var volume = new SinkVolume(rig.Session);
            var changes = 0;
            volume.Changed += _ => changes++;
            Assert.True(rig.Session.WaitFor(() => volume.IsAvailable, 5000), "the default sink never reported its volume");
            Assert.Equal(AudioDaemonFixture.SinkName, volume.SinkName);
            Assert.False(volume.UsesRoute);

            using var observerSession = AudioSession.TryCreate(rig.Loop, out var reason, daemon.SocketPath, "basin-test-observer")
                ?? throw new InvalidOperationException(reason);
            using var observer = new SinkVolume(observerSession);
            Assert.True(observerSession.WaitFor(() => observer.IsAvailable, 5000));

            changes = 0;
            Assert.True(volume.SetVolume(0.5f));
            Assert.True(rig.Session.WaitFor(() => MathF.Abs(volume.Volume - 0.5f) < 0.001f, 5000), $"volume reads {volume.Volume}");
            Assert.Equal(0.125f, volume.LinearVolume, 4);
            Assert.True(changes >= 1);
            Assert.True(observerSession.WaitFor(() => MathF.Abs(observer.Volume - 0.5f) < 0.001f, 5000), $"the observer reads {observer.Volume}");

            Assert.True(volume.SetMuted(true));
            Assert.True(rig.Session.WaitFor(() => volume.Muted, 5000));
            Assert.True(observerSession.WaitFor(() => observer.Muted, 5000));
            Assert.Equal(0.5f, volume.Volume, 3);

            Assert.True(volume.SetVolume(2f));
            Assert.True(rig.Session.WaitFor(() => MathF.Abs(volume.Volume - SinkVolume.MaxVolume) < 0.001f, 5000), $"volume reads {volume.Volume}");

            Assert.True(volume.SetMuted(false));
            Assert.True(volume.SetVolume(1f));
            Assert.True(rig.Session.WaitFor(() => !volume.Muted && MathF.Abs(volume.Volume - 1f) < 0.001f, 5000));
        }
        finally
        {
            rig.Session.DefaultMetadata.SetProperty(0, DefaultSinkKey, null, null);
            Assert.True(rig.Session.Roundtrip());
        }
    }

    [Fact]
    public void Without_a_default_sink_the_volume_is_unavailable_and_refuses_to_write()
    {
        daemon.SkipIfUnavailable();
        using var rig = new AudioRig(daemon);
        Assert.True(rig.Session.Roundtrip());
        using var volume = new SinkVolume(rig.Session);
        rig.Pump(TimeSpan.FromMilliseconds(200));

        Assert.Null(volume.SinkName);
        Assert.False(volume.IsAvailable);
        Assert.False(volume.SetVolume(0.5f));
        Assert.False(volume.SetMuted(true));
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(0.125f, 0.5f)]
    [InlineData(1f, 1f)]
    [InlineData(0.001f, 0.1f)]
    public void Volume_is_the_cube_root_of_the_linear_gain(float linear, float cubic)
    {
        Assert.Equal(cubic, SinkVolume.ToCubic(linear), 4);
        Assert.Equal(linear, SinkVolume.ToLinear(cubic), 4);
    }

    [Fact]
    public unsafe void A_route_volume_names_the_route_and_carries_the_props_and_save()
    {
        using var builder = new SpaPodBuilder(512);
        SinkVolume.BuildRoute(builder, 3, 7, [0.25f, 0.5f], true);
        fixed (byte* bytes = builder.WrittenSpan)
        {
            var pod = new SpaPod((spa_pod*)bytes);
            Assert.True(SinkVolume.ReadRoute(pod, out var index, out var device, out var props));
            Assert.Equal(3, index);
            Assert.Equal(7, device);
            float[] volumes = [];
            var muted = false;
            Assert.True(SinkVolume.ReadProps(props, ref volumes, ref muted));
            Assert.Equal([0.25f, 0.5f], volumes);
            Assert.True(muted);
            Assert.Equal(0.375f, SinkVolume.Average(volumes));
            Assert.True(pod.AsObject().TryGetValue((uint)spa_param_route.SPA_PARAM_ROUTE_save, out var save) && save.AsBool());
        }

        builder.Reset();
        using (builder.PushObject(SPA_TYPE_OBJECT_ParamRoute, (uint)spa_param_type.SPA_PARAM_Route))
        {
            builder.AddProperty((uint)spa_param_route.SPA_PARAM_ROUTE_index);
            builder.AddInt(1);
            builder.AddProperty((uint)spa_param_route.SPA_PARAM_ROUTE_direction);
            builder.AddId((uint)spa_direction.SPA_DIRECTION_INPUT);
            builder.AddProperty((uint)spa_param_route.SPA_PARAM_ROUTE_device);
            builder.AddInt(2);
        }

        fixed (byte* bytes = builder.WrittenSpan)
        {
            Assert.False(SinkVolume.ReadRoute(new SpaPod((spa_pod*)bytes), out _, out _, out _));
        }
    }

    [Fact]
    public unsafe void A_mute_alone_writes_no_channel_volumes()
    {
        using var builder = new SpaPodBuilder(256);
        SinkVolume.BuildProps(builder, [], false);
        fixed (byte* bytes = builder.WrittenSpan)
        {
            var props = new SpaPod((spa_pod*)bytes).AsObject();
            Assert.False(props.TryGetValue((uint)spa_prop.SPA_PROP_channelVolumes, out _));
            Assert.True(props.TryGetValue((uint)spa_prop.SPA_PROP_mute, out var mute));
            Assert.False(mute.AsBool());
        }
    }
}
